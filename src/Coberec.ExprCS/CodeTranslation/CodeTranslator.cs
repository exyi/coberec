using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Coberec.CSharpGen;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Util;
using Xunit;
using IL=ICSharpCode.Decompiler.IL;

namespace Coberec.ExprCS.CodeTranslation
{
    partial class CodeTranslator
    {
        public static ILFunction CreateBody(MethodDef method, IMethod generatedMethod, MetadataContext cx)
        {
            var translator = new CodeTranslator(method, generatedMethod, cx);
            if (!method.Signature.IsStatic)
            {
                Assert.Equal(method.Signature.DeclaringType, method.ArgumentParams.First().Type);
                translator.Parameters.Add(
                    method.ArgumentParams.First().Id,
                    new ILVariable(VariableKind.Parameter, cx.GetTypeDef(method.Signature.DeclaringType), -1) { Name = "this" });
            }
            foreach (var (i, (arg, param)) in method.ArgumentParams.Skip(method.Signature.IsStatic ? 0 : 1).ZipTuples(method.Signature.Args).Indexed())
            {
                Assert.Equal(arg.Type, param.Type);
                translator.Parameters.Add(
                    arg.Id,
                    new ILVariable(VariableKind.Parameter, cx.GetTypeReference(param.Type), i) { Name = arg.Name });
            }
            var verificationVarCopy = translator.Parameters.Keys.ToHashSet();

            var statements = translator.TranslateExpression(method.Body);

            Assert.Empty(translator.BreakTargets);
            Assert.Equal(verificationVarCopy, translator.Parameters.Keys.ToHashSet());

            return translator.BuildTheFunction(statements);
        }

        private CodeTranslator(MethodDef method, IMethod generatedMethod, MetadataContext cx)
        {
            this.Method = method;
            this.GeneratedMethod = generatedMethod;
            this.Metadata = cx;
        }

        readonly MetadataContext Metadata;
        readonly MethodDef Method;
        readonly IMethod GeneratedMethod;
        readonly Dictionary<Guid, ILVariable> Parameters = new Dictionary<Guid, ILVariable>();
        readonly Dictionary<LabelTarget, (Block nextBlock, ILVariable resultVariable)> BreakTargets = new Dictionary<LabelTarget, (Block nextBlock, ILVariable resultVariable)>();

        ILInstruction ToILInstruction(Result r, ILVariable resultVar)
        {
            Result copyOutput(ILVariable output)
            {
                if (output == null)
                {
                    Assert.Null(resultVar);
                    return Result.Nop;
                }
                else
                {
                    Assert.NotNull(resultVar);
                    return new Result(new ExpressionStatement(new StLoc(resultVar, new LdLoc(output))));
                }
            }

            if (r.Output == null && r.Statements.Count == 1 && r.Statements[0] is ExpressionStatement expr)
            {
                Assert.Null(expr.Output);
                Assert.Null(resultVar);
                return expr.Instruction;
            }

            return this.BuildBContainer(
                Result.Concat(r, copyOutput(r.Output)));
        }

        BlockContainer BuildBContainer(Result r)
        {
            var isVoid = r.Output == null;
            var container = new IL.BlockContainer(expectedResultType: isVoid ? IL.StackType.Void : r.Output.StackType);
            var block = new IL.Block();

            foreach (var stmt in r.Statements)
            {
                if (stmt is ExpressionStatement e)
                {
                    var instruction = e.Output == null ? e.Instruction : new StLoc(e.Output, e.Instruction);
                    block.Instructions.Add(instruction);
                }
                else if (stmt is BasicBlockStatement bb)
                {
                    block.AddLeaveInstruction(bb.Block);
                    container.Blocks.Add(block);

                    var nextBlock = new Block();
                    bb.Block.AddLeaveInstruction(nextBlock);
                    container.Blocks.Add(bb.Block);

                    block = nextBlock;
                }
                else throw new NotSupportedException($"Statement {stmt} of type {stmt.GetType()}");
            }

            if (block.HasReachableEndpoint())
            {
                if (isVoid) block.Instructions.Add(new IL.Leave(container));
                else        block.Instructions.Add(new IL.Leave(container, value: new LdLoc(r.Output)));
            }

            container.Blocks.Add(block);


            // compute fake IL ranges, just to have some IDs of the Blocks
            var index = 0;
            foreach (var b in container.Blocks)
            {
                var length = b.Instructions.Count;
                b.SetILRange(new Interval(index, index + length));
                index += length;
            }

            container.SetILRange(new Interval(0, index));

            // this computes the flags of the container. hopefully it does not break anything...
            container.AddRef();

            return container;
        }

        ILFunction BuildTheFunction(Result r)
        {
            var isVoid = GeneratedMethod.ReturnType.FullName == "System.Void";

            Assert.Equal(isVoid, r.Output == null);

            var functionContainer = this.BuildBContainer(r);

            var variables = new VariableCollectingVisitor();
            r.Output?.Apply(variables.Variables.Add);
            foreach (var p in this.Parameters)
                variables.Variables.Add(p.Value);

            functionContainer.AcceptVisitor(variables);

            var ilFunc = new IL.ILFunction(this.GeneratedMethod, 10000, new ICSharpCode.Decompiler.TypeSystem.GenericContext(), functionContainer);

            foreach (var i in variables.Variables)
                if (i.Function == null)
                    ilFunc.Variables.Add(i);

            ilFunc.AddRef(); // whatever, somehow initializes the freaking tree
            ilFunc.CheckInvariantPublic(IL.ILPhase.Normal);
            return ilFunc;
        }

        class VariableCollectingVisitor : IL.ILVisitor
        {
            public readonly HashSet<IL.ILVariable> Variables = new HashSet<IL.ILVariable>();

            protected override void Default(IL.ILInstruction inst)
            {
                if (inst is IL.IInstructionWithVariableOperand a)
                    Variables.Add(a.Variable);

                foreach(var c in inst.Children)
                    c.AcceptVisitor(this);
            }
        }

        Result TranslateExpression(Expression expr) =>
            expr.Match(
                e => TranslateBinary(e.Item),
                e => TranslateNot(e.Item),
                e => TranslateMethodCall(e.Item),
                e => TranslateNewObject(e.Item),
                e => TranslateFieldAccess(e.Item),
                e => TranslateFieldAssign(e.Item),
                e => TranslateNumericConversion(e.Item),
                e => TranslateReferenceConversion(e.Item),
                e => TranslateConstant(e.Item),
                e => TranslateDefault(e.Item),
                e => TranslateParameter(e.Item),
                e => TranslateConditional(e.Item),
                e => TranslateFunction(e.Item),
                e => TranslateInvoke(e.Item),
                e => TranslateBreak(e.Item),
                e => TranslateBreakable(e.Item),
                e => TranslateLoop(e.Item),
                e => TranslateLetIn(e.Item),
                e => TranslateBlock(e.Item),
                e => TranslateLowerable(e.Item));

        Result TranslateNot(NotExpression item)
        {
            var r = this.TranslateExpression(item.Expr);
            Assert.NotNull(r.Output);
            var expr = Comp.LogicNot(new LdLoc(r.Output));
            return Result.Concat(
                r,
                Result.Expression(r.Output.Type, expr)
            );
        }

        Result TranslateLowerable(LowerableExpression e) =>
            this.TranslateExpression(e.Lowered);

        Result TranslateBlock(BlockExpression block) =>
            block.Expressions.Append(block.Result).Select(TranslateExpression).Apply(Result.Concat);

        Result TranslateLetIn(LetInExpression e)
        {
            var value = this.TranslateExpression(e.Value);

            Assert.NotNull(value.Output);
            Assert.NotEqual(TypeSignature.Void, e.Variable.Type); // TODO: add support for voids?
            Assert.Equal(value.Output.Type, this.Metadata.GetTypeReference(e.Variable.Type));

            var ilVar = new ILVariable(VariableKind.Local, value.Output.Type);
            ilVar.Name = e.Variable.Name;
            this.Parameters.Add(e.Variable.Id, ilVar);
            var target = this.TranslateExpression(e.Target);
            this.Parameters.Remove(e.Variable.Id);
            return Result.Concat(
                value,
                new Result(ImmutableList.Create<Statement>(new ExpressionStatement(new StLoc(ilVar, new LdLoc(value.Output))))),
                target);
        }

        Result TranslateLoop(LoopExpression e)
        {
            var startBlock = new Block();
            var expr = this.TranslateExpression(e.Body);

            var bc = this.BuildBContainer(
                Result.Concat(
                    new Result(
                        new BasicBlockStatement(startBlock)
                    ),
                    expr,
                    new Result(
                        new ExpressionStatement(new Branch(startBlock))
                    )
                ));
            return new Result(new ExpressionStatement(bc));
        }

        Result TranslateBreakable(BreakableExpression e)
        {
            var endBlock = new Block();
            var resultVariable = this.CreateOutputVar(e.Label.Type);
            this.BreakTargets.Add(e.Label, (endBlock, resultVariable));
            var expr = this.TranslateExpression(e.Expression);
            Assert.True(this.BreakTargets.Remove(e.Label));

            return
                Result.Concat(
                    expr,
                    new Result(
                        resultVariable,
                        new BasicBlockStatement(endBlock)
                    )
                );
        }

        ILVariable CreateOutputVar(TypeReference type) =>
            type == TypeSignature.Void ?
                null :
                new ILVariable(VariableKind.Local, this.Metadata.GetTypeReference(type));

        Result TranslateBreak(BreakExpression e)
        {
            var (endBlock, resultVar) = this.BreakTargets[e.Target];
            var breakStmt = new ExpressionStatement(new IL.Branch(endBlock));
            Assert.Equal(e.Target.Type, e.Value.Type());
            if (e.Target.Type == TypeSignature.Void)
            {
                Assert.Null(resultVar);
                var value = this.TranslateExpression(e.Value);
                Assert.Null(value.Output);
                return Result.Concat(
                    value,
                    new Result(ImmutableList.Create(breakStmt))
                );
            }
            else
            {
                var value = this.TranslateExpression(e.Value);

                Assert.NotNull(value.Output);
                Assert.NotNull(resultVar);

                return Result.Concat(
                    value,
                    new Result(ImmutableList.Create(
                        new ExpressionStatement(output: resultVar, instruction: new LdLoc(value.Output)),
                        breakStmt
                    ))
                );
            };
        }

        private static Result TranslateInvoke(InvokeExpression item)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateFunction(FunctionExpression item)
        {
            throw new NotImplementedException();
        }

        Result TranslateConditional(ConditionalExpression item)
        {
            // TODO: shortcut for simplest expressions
            Assert.Equal(item.IfTrue.Type(), item.IfFalse.Type());

            var condition = this.TranslateExpression(item.Condition);
            Assert.NotNull(condition.Output);
            var ifTrue = this.TranslateExpression(item.IfTrue);
            var ifFalse = this.TranslateExpression(item.IfFalse);

            var resultVar = this.CreateOutputVar(item.IfTrue.Type());

            var ifTrueC = this.ToILInstruction(ifTrue, resultVar);
            var ifFalseC = ifFalse.IsNop ? null : this.ToILInstruction(ifFalse, resultVar);

            return Result.Concat(
                condition,
                new Result(
                    output: resultVar,
                    new ExpressionStatement(new IfInstruction(new LdLoc(condition.Output), ifTrueC, ifFalseC)))
            );

            // var elseBlock = new Block();
            // var endBlock = new Block();
            // var condInstruction = new IfInstruction(new LdLoc(condition.Output), new Nop(), new Branch(elseBlock));

            // var isVoid = ifTrue.Output == null;
            // Assert.Equal(isVoid, ifFalse == null);
            // Assert.Equal(isVoid, item.IfFalse.Type() == TypeSignature.Void);

            // var outputVar = isVoid ? null : new ILVariable(VariableKind.StackSlot, cx.Metadata.GetTypeReference(item.IfTrue.Type()));

            // return Result.Concat(
            //     condition,
            //     new Result(new ExpressionStatement(condInstruction)),

            //     ifTrue,
            //     isVoid ? Result.Nop : new Result(new ExpressionStatement(new LdLoc(ifTrue.Output), outputVar)),
            //     new Result(new BasicBlockStatement(new Block { Instructions = { new Branch(endBlock) } })),

            //     new Result(new BasicBlockStatement(elseBlock)),
            //     ifFalse,
            //     isVoid ? Result.Nop : new Result(new ExpressionStatement(new LdLoc(ifFalse.Output), outputVar)),

            //     new Result(new BasicBlockStatement(endBlock)),
            //     isVoid ? Result.Nop : new Result(outputVar)
            // );
        }

        Result TranslateParameter(ParameterExpression pe)
        {
            var v = this.Parameters[pe.Id];
            return new Result(v);
        }

        Result TranslateDefault(DefaultExpression e)
        {
            if (e.Type == TypeSignature.Void)
                return new Result();
            else
            {
                var type = this.Metadata.GetTypeReference(e.Type);
                return Result.Expression(type, new IL.DefaultValue(type));
            }
        }

        Result TranslateReferenceConversion(ReferenceConversionExpression e)
        {
            // TODO: validate

            var value = this.TranslateExpression(e.Value);
            var to = this.Metadata.GetTypeReference(e.Type);

            var conversion = this.Metadata.CSharpConversions.ExplicitConversion(value.Output.Type, to);
            if (!conversion.IsValid)
                throw new Exception($"There isn't any valid conversion from {value.Output.Type} to {to}.");
            if (!conversion.IsReferenceConversion)
                throw new Exception($"There is not a reference conversion from {value.Output.Type} to {to}, but an '{conversion}' was found");

            // reference conversions in IL code are simply omitted...
            return Result.Concat(
                value,
                Result.Expression(to, new LdLoc(value.Output))
            );
        }

        Result TranslateNumericConversion(NumericConversionExpression e)
        {
            var to = this.Metadata.GetTypeReference(e.Type);

            var targetPrimitive = to.GetDefinition().KnownTypeCode.ToPrimitiveType();
            if (targetPrimitive == PrimitiveType.None)
                throw new NotSupportedException($"Primitive type {to} is not supported.");

            var value = this.TranslateExpression(e.Value);

            var expr = new Conv(new LdLoc(value.Output), targetPrimitive, e.Checked, value.Output.Type.GetSign());

            return Result.Concat(
                value,
                Result.Expression(to, expr)
            );
        }

        Result TranslateFieldAssign(FieldAssignExpression e)
        {
            // TODO: unit test
            var field = this.Metadata.GetField(e.Field);
            var target = e.Target?.Apply(this.TranslateExpression);
            var value = this.TranslateExpression(e.Value);

            var load = new StObj(ILAstFactory.FieldAddr(field, target.Output), new LdLoc(value.Output), field.Type);

            return Result.Concat(
                target,
                value,
                Result.Expression(field.Type, load)
            );
        }

        Result TranslateFieldAccess(FieldAccessExpression e)
        {
            // TODO: unit test
            var field = this.Metadata.GetField(e.Field);
            var target = e.Target?.Apply(TranslateExpression);

            var load = new LdObj(ILAstFactory.FieldAddr(field, target.Output), field.Type);

            return Result.Concat(
                target,
                Result.Expression(field.Type, load)
            );
        }

        Result TranslateNewObject(NewObjectExpression e)
        {
            // TODO: unit test
            var method = this.Metadata.GetMethod(e.Ctor);

            var args = e.Args.Select(this.TranslateExpression).ToArray();

            var call = new NewObj(method);
            call.Arguments.AddRange(args.Select(a => new LdLoc(a.Output)));

            return Result.Concat(args.Append(Result.Expression(method.DeclaringType, call)));
        }

        Result TranslateMethodCall(MethodCallExpression e)
        {
            Assert.Equal(e.Method.IsStatic, e.Target == null);
            Assert.Equal(e.Method.Args.Length, e.Args.Length);

            var method = this.Metadata.GetMethod(e.Method);

            var args = e.Args.Select(TranslateExpression).ToArray();
            var target = e.Target?.Apply(TranslateExpression);

            var result = new List<Result>();
            target?.ApplyAction(result.Add);
            result.AddRange(args);

            var call = method.IsStatic ? new Call(method) : (CallInstruction)new CallVirt(method) { Arguments = { new LdLoc(target.Output) } };
            call.Arguments.AddRange(args.Select(a => new LdLoc(a.Output)));
            var isVoid = e.Method.ResultType == TypeSignature.Void;
            result.Add(isVoid ?
                       new Result(new ExpressionStatement(call)) :
                       Result.Expression(method.ReturnType, call));

            return Result.Concat(result);
        }

        Result TranslateBinary(BinaryExpression item)
        {
            throw new NotImplementedException();
        }

        Result TranslateConstant(ConstantExpression e)
        {
            var type = this.Metadata.GetTypeReference(e.Type);
            return Result.Expression(type, ILAstFactory.Constant(e.Value, type));
        }

        class Statement
        {
        }

        class BasicBlockStatement : Statement
        {
            public Block Block;
            public BasicBlockStatement(Block b)
            {
                this.Block = b ?? throw new ArgumentNullException(nameof(b));
            }
        }

        class ExpressionStatement : Statement
        {
            public readonly ILVariable Output;
            public readonly ILInstruction Instruction;

            public ExpressionStatement(ILInstruction instruction, ILVariable output = null)
            {
                Output = output;
                Instruction = instruction ?? throw new ArgumentNullException(nameof(instruction));
            }
        }

        class Result
        {
            public readonly ImmutableList<Statement> Statements;
            public readonly ILVariable Output;

            public static Result Nop = new Result();
            public bool IsNop => this.Statements.Count == 0 && Output is null;

            public static Result Expression(IType type, ILInstruction i)
            {
                var resultVar = new ILVariable(VariableKind.StackSlot, type);
                return new Result(output: resultVar, new ExpressionStatement(i, resultVar));
            }

            public Result(params Statement[] statements) : this(null, statements) { }
            public Result(IEnumerable<Statement> statements) : this(null, statements) { }
            public Result(ILVariable output, params Statement[] statements) : this(output, statements.AsEnumerable()) { }
            public Result(ILVariable output, IEnumerable<Statement> statements)
            {
                this.Statements = statements.ToImmutableList();
                this.Statements.ForEach(Assert.NotNull);
                Assert.NotEqual(StackType.Void, output?.StackType);
                this.Output = output;
            }

            public static Result Concat(params Result[] rs) => Concat((IEnumerable<Result>)rs);
            public static Result Concat(IEnumerable<Result> rs)
            {
                var s = ImmutableList<Statement>.Empty;
                ILVariable output = null;
                foreach (var r in rs)
                {
                    if (r.Statements != null) s = s.AddRange(r.Statements);
                    output = r.Output;
                }
                return new Result(output, s);
            }
        }
    }
}
