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
    public static class CodeTranslator
    {
        public static ILFunction CreateBody(MethodDef method, IMethod generatedMethod, MetadataContext cx)
        {
            var methodCx = new MethodContext { Method = method, Metadata = cx, GeneratedMethod = generatedMethod };
            if (!method.Signature.IsStatic)
            {
                Assert.Equal(method.Signature.DeclaringType, method.ArgumentParams.First().Type);
                methodCx.Parameters.Add(
                    method.ArgumentParams.First().Id,
                    new ILVariable(VariableKind.Parameter, cx.GetTypeDef(method.Signature.DeclaringType), -1) { Name = "this" });
            }
            foreach (var (i, (arg, param)) in method.ArgumentParams.Skip(method.Signature.IsStatic ? 0 : 1).ZipTuples(method.Signature.Args).Indexed())
            {
                Assert.Equal(arg.Type, param.Type);
                methodCx.Parameters.Add(
                    arg.Id,
                    new ILVariable(VariableKind.Parameter, cx.GetTypeReference(param.Type), i) { Name = arg.Name });
            }
            var verificationVarCopy = methodCx.Parameters.Keys.ToHashSet();

            var statements = TranslateExpression(method.Body, methodCx);

            Assert.Empty(methodCx.BreakTargets);
            Assert.Equal(verificationVarCopy, methodCx.Parameters.Keys.ToHashSet());

            return BuildTheFunction(statements, methodCx);
        }

        static void AddLeaveInstruction(this Block b, Block nextBlock)
        {
            if (b.HasReachableEndpoint())
                b.Instructions.Add(new Branch(nextBlock));
        }

        static bool HasReachableEndpoint(this Block b)
        {
            return b.Instructions.LastOrDefault()?.HasFlag(InstructionFlags.EndPointUnreachable) != true;
        }

        static BlockContainer BuildBContainer(Result r, MethodContext cx)
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

        static ILFunction BuildTheFunction(Result r, MethodContext cx)
        {
            var isVoid = cx.GeneratedMethod.ReturnType.FullName == "System.Void";

            Assert.Equal(isVoid, r.Output == null);

            var functionContainer = BuildBContainer(r, cx);

            var variables = new VariableCollectingVisitor();
            r.Output?.Apply(variables.Variables.Add);
            foreach (var p in cx.Parameters)
                variables.Variables.Add(p.Value);

            functionContainer.AcceptVisitor(variables);

            var ilFunc = new IL.ILFunction(cx.GeneratedMethod, 10000, new ICSharpCode.Decompiler.TypeSystem.GenericContext(), functionContainer);

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

        static Result TranslateExpression(Expression expr, MethodContext cx) =>
            expr.Match(
                e => TranslateBinary(e.Item, cx),
                e => TranslateMethodCall(e.Item, cx),
                e => TranslateNewObject(e.Item, cx),
                e => TranslateFieldAccess(e.Item, cx),
                e => TranslateFieldAssign(e.Item, cx),
                e => TranslateNumericConversion(e.Item, cx),
                e => TranslateReferenceConversion(e.Item, cx),
                e => TranslateConstant(e.Item, cx),
                e => TranslateDefault(e.Item, cx),
                e => TranslateParameter(e.Item, cx),
                e => TranslateConditional(e.Item, cx),
                e => TranslateFunction(e.Item, cx),
                e => TranslateInvoke(e.Item, cx),
                e => TranslateBreak(e.Item, cx),
                e => TranslateBreakable(e.Item, cx),
                e => TranslateLoop(e.Item, cx),
                e => TranslateLetIn(e.Item, cx),
                e => TranslateBlock(e.Item, cx),
                e => TranslateLowerable(e.Item, cx));

        private static Result TranslateLowerable(LowerableExpression e, MethodContext cx) =>
            TranslateExpression(e.Lowered, cx);

        private static Result TranslateBlock(BlockExpression block, MethodContext cx)
        {
            return block.Expressions.Append(block.Result).Select(e => TranslateExpression(e, cx)).Apply(Result.Concat);
        }

        private static Result TranslateLetIn(LetInExpression e, MethodContext cx)
        {
            var value = TranslateExpression(e.Value, cx);

            Assert.NotNull(value.Output);
            Assert.Equal(value.Output.Type, cx.Metadata.GetTypeReference(e.Variable.Type));

            var ilVar = new ILVariable(VariableKind.Local, value.Output.Type);
            ilVar.Name = e.Variable.Name;
            cx.Parameters.Add(e.Variable.Id, ilVar);
            var target = TranslateExpression(e.Target, cx);
            cx.Parameters.Remove(e.Variable.Id);
            return Result.Concat(
                value,
                new Result(ImmutableList.Create<Statement>(new ExpressionStatement(new StLoc(ilVar, new LdLoc(value.Output))))),
                target);
        }

        private static Result TranslateLoop(LoopExpression e, MethodContext cx)
        {
            var startBlock = new Block();
            var expr = TranslateExpression(e.Body, cx);

            var bc = BuildBContainer(
                Result.Concat(
                    new Result(
                        new BasicBlockStatement(startBlock)
                    ),
                    expr,
                    new Result(
                        new ExpressionStatement(new Branch(startBlock))
                    )
                ),
                cx);
            return new Result(new ExpressionStatement(bc));
        }

        private static Result TranslateBreakable(BreakableExpression e, MethodContext cx)
        {
            var endBlock = new Block();
            var resultVariable = e.Label.Type == TypeSignature.Void ?
                                 null :
                                 new ILVariable(VariableKind.Local, cx.Metadata.GetTypeReference(e.Label.Type));
            var expr = TranslateExpression(e.Expression, cx);

            return
                Result.Concat(
                    expr,
                    new Result(
                        resultVariable,
                        new BasicBlockStatement(endBlock)
                    )
                );
        }

        private static Result TranslateBreak(BreakExpression e, MethodContext cx)
        {
            var (endBlock, resultVar) = cx.BreakTargets[e.Target];
            var breakStmt = new ExpressionStatement(new IL.Branch(endBlock));
            Assert.Equal(e.Target.Type, e.Value.Type());
            if (e.Target.Type == TypeSignature.Void)
            {
                Assert.Null(resultVar);
                var value = TranslateExpression(e.Value, cx);
                Assert.Null(value.Output);
                return Result.Concat(
                    value,
                    new Result(ImmutableList.Create(breakStmt))
                );
            }
            else
            {
                var value = TranslateExpression(e.Value, cx);

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

        private static Result TranslateInvoke(InvokeExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateFunction(FunctionExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateConditional(ConditionalExpression item, MethodContext cx)
        {
            // TODO: shortcut for simplest expressions
            Assert.Equal(item.IfTrue.Type(), item.IfFalse.Type());

            var condition = TranslateExpression(item.Condition, cx);
            Assert.NotNull(condition.Output);
            var ifTrue = TranslateExpression(item.IfTrue, cx);
            var ifFalse = TranslateExpression(item.IfFalse, cx);

            var resultVar = new ILVariable(VariableKind.Local, cx.Metadata.GetTypeReference(item.IfTrue.Type()));

            Result copyOutput(ILVariable output) => output == null ? Result.Nop : new Result(new ExpressionStatement(new StLoc(resultVar, new LdLoc(ifTrue.Output))));

            var ifTrueC = BuildBContainer(
                Result.Concat(ifTrue, copyOutput(ifTrue.Output)),
                cx);
            var ifFalseC = BuildBContainer(
                Result.Concat(ifFalse, copyOutput(ifFalse.Output)),
                cx);

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

        private static Result TranslateParameter(ParameterExpression pe, MethodContext cx)
        {
            var v = cx.Parameters[pe.Id];
            return new Result(v);
        }

        private static Result TranslateDefault(DefaultExpression e, MethodContext cx)
        {
            if (e.Type == TypeSignature.Void)
                return new Result();
            else
            {
                var type = cx.Metadata.GetTypeReference(e.Type);
                return Result.Expression(type, new IL.DefaultValue(type));
            }
        }

        private static Result TranslateReferenceConversion(ReferenceConversionExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateNumericConversion(NumericConversionExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateFieldAssign(FieldAssignExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateFieldAccess(FieldAccessExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateNewObject(NewObjectExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateMethodCall(MethodCallExpression e, MethodContext cx)
        {
            Assert.Equal(e.Method.IsStatic, e.Target == null);
            Assert.Equal(e.Method.Args.Length, e.Args.Length);

            var method = cx.Metadata.GetMethod(e.Method);

            var args = e.Args.Select(a => TranslateExpression(a, cx)).ToArray();
            var target = e.Target?.Apply(t => TranslateExpression(t, cx));

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

        private static Result TranslateBinary(BinaryExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateConstant(ConstantExpression e, MethodContext cx)
        {
            var type = cx.Metadata.GetTypeReference(e.Type);
            return Result.Expression(type, TranslateConstant(e.Value, type));
        }

        private static ILInstruction TranslateConstant(object constant, IType type)
        {
            if (constant == null)
            {
                Assert.True(type.IsReferenceType);
                return new LdNull();
            }
            else if (constant is bool boolC)
            {
                Assert.Equal(typeof(bool).FullName, type.FullName);
                return new IL.LdcI4(boolC ? 1 : 0);
            }
            else if (constant is char || constant is byte || constant is sbyte || constant is ushort || constant is short || constant is int)
            {
                return new IL.LdcI4(Convert.ToInt32(constant));
            }
            else if (constant is uint uintC)
            {
                return new IL.LdcI4(unchecked((int)uintC));
            }
            else if (constant is long longC)
                return new IL.LdcI8(longC);
            else if (constant is ulong ulongC)
                return new IL.LdcI8(unchecked((long)ulongC));
            else if (constant is float floatC)
                return new LdcF4(floatC);
            else if (constant is double doubleC)
                return new LdcF8(doubleC);
            else if (constant is decimal decimalC)
                return new LdcDecimal(decimalC);
            else if (constant is string stringC)
                return new IL.LdStr(stringC);
            else
                throw new NotSupportedException($"Constant '{constant}' of type '{constant.GetType()}' with declared type '{type}' is not supported.");
        }

        class MethodContext
        {
            public MetadataContext Metadata;
            public MethodDef Method;
            internal IMethod GeneratedMethod;
            public Dictionary<Guid, ILVariable> Parameters = new Dictionary<Guid, ILVariable>();
            public Dictionary<LabelTarget, (Block nextBlock, ILVariable resultVariable)> BreakTargets = new Dictionary<LabelTarget, (Block nextBlock, ILVariable resultVariable)>();
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
