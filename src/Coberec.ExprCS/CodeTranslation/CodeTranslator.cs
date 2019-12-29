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
using TS=ICSharpCode.Decompiler.TypeSystem;

namespace Coberec.ExprCS.CodeTranslation
{
    partial class CodeTranslator
    {
        public static ILFunction CreateBody(MethodDef method, IMethod generatedMethod, MetadataContext cx)
        {
            Assert.NotNull(method.Body);
            Assert.False(method.ArgumentParams.IsDefault);
            if (method.Body is ILSpyMethodBody iLSpyMethod)
                return iLSpyMethod.BuildBody(generatedMethod, cx);

            var translator = new CodeTranslator(method, generatedMethod, cx);
            var declaringType = method.Signature.DeclaringType;
            if (!method.Signature.IsStatic)
            {
                var firstArgument = method.ArgumentParams.First();
                if (declaringType.IsValueType)
                    Assert.Equal(TypeReference.ByReferenceType(declaringType), firstArgument.Type);
                else
                    Assert.Equal(declaringType, firstArgument.Type);

                translator.Parameters.Add(
                    firstArgument.Id,
                    new ILVariable(VariableKind.Parameter, cx.GetTypeReference(firstArgument.Type), -1) { Name = "this" });
            }
            foreach (var (i, (arg, param)) in method.ArgumentParams.Skip(method.Signature.IsStatic ? 0 : 1).ZipTuples(method.Signature.Params).Indexed())
            {
                Assert.Equal(arg.Type, param.Type);
                translator.Parameters.Add(
                    arg.Id,
                    new ILVariable(VariableKind.Parameter, cx.GetTypeReference(param.Type), i) { Name = generatedMethod.Parameters[i].Name });
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
        readonly Dictionary<Guid, ILFunction> ActiveLocalFunctions = new Dictionary<Guid, ILFunction>();
        readonly Dictionary<LabelTarget, (Block nextBlock, ILVariable resultVariable)> BreakTargets = new Dictionary<LabelTarget, (Block nextBlock, ILVariable resultVariable)>();
        /// Local function that will need to be added into the nearest parent function
        List<ILFunction> PendingLocalFunctions = new List<ILFunction>();

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
                else        block.Instructions.Add(new IL.Leave(container, value: r.LdOutput()));
            }

            container.Blocks.Add(block);

            foreach (var f in this.PendingLocalFunctions)
                if (f.DeclarationScope is null)
                    f.DeclarationScope = container;


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
            var functionContainer = this.BuildBContainer(r);
            var fn = ILAstFactory.CreateFunction(this.GeneratedMethod, functionContainer, this.Parameters.Select(p => p.Value));
            fn.LocalFunctions.AddRange(this.PendingLocalFunctions);

            fn.AddRef(); // whatever, somehow initializes the freaking tree
            fn.CheckInvariantPublic(ILPhase.Normal);
            return fn;
        }

        Result TranslateExpression(Expression expr)
        {
            Result result = expr.Match(
                e => TranslateBinary(e.Item),
                e => TranslateNot(e.Item),
                e => TranslateMethodCall(e.Item),
                e => TranslateNewObject(e.Item),
                e => TranslateFieldAccess(e.Item),
                e => TranslateReferenceAssign(e.Item),
                e => TranslateDereference(e.Item),
                e => TranslateVariableReference(e.Item),
                e => TranslateAddressOf(e.Item),
                e => TranslateNumericConversion(e.Item),
                e => TranslateReferenceConversion(e.Item),
                e => TranslateConstant(e.Item),
                e => TranslateDefault(e.Item),
                e => TranslateParameter(e.Item),
                e => TranslateConditional(e.Item),
                e => TranslateFunction(e.Item),
                e => TranslateFunctionConversion(e.Item),
                e => TranslateInvoke(e.Item),
                e => TranslateBreak(e.Item),
                e => TranslateBreakable(e.Item),
                e => TranslateLoop(e.Item),
                e => TranslateLetIn(e.Item),
                e => TranslateNewArray(e.Item),
                e => TranslateArrayIndex(e.Item),
                e => TranslateBlock(e.Item),
                e => TranslateLowerable(e.Item));
            var expectedType = expr.Type();
            if (expectedType == TypeSignature.Void)
                Assert.Null(result.Output);
            else
                Assert.NotNull(result.Output);
            if (expectedType is TypeReference.FunctionTypeCase fn)
            {
                Assert.Equal(StackType.O, result.Output.StackType);
                var invokeMethod = result.Output.Type.GetDelegateInvokeMethod();
                Assert.NotNull(invokeMethod);
                Assert.Equal(invokeMethod.Parameters.Count, fn.Item.Params.Length);
                Assert.Equal(invokeMethod.Parameters.Select(p => SymbolLoader.TypeRef(p.Type)), fn.Item.Params.Select(p => p.Type));
                Assert.Equal(SymbolLoader.TypeRef(invokeMethod.ReturnType), fn.Item.ResultType);
            }

            else if (expectedType != TypeSignature.Void)
            {
                var t = this.Metadata.GetTypeReference(expectedType);
                Assert.Equal(t.FullName, result.Output.Type.FullName);
            }

            return result;
        }

        
        Result TranslateNot(NotExpression item)
        {
            var r = this.TranslateExpression(item.Expr);
            Assert.NotNull(r.Output);
            var expr = Comp.LogicNot(r.LdOutput());
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
            if (e.Value is Expression.FunctionCase fn)
                return TranslateLocalFunction(fn.Item, e.Variable, e.Target);

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
                new Result(ImmutableList.Create<Statement>(new ExpressionStatement(new StLoc(ilVar, value.LdOutput())))),
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
                        new ExpressionStatement(output: resultVar, instruction: value.LdOutput()),
                        breakStmt
                    ))
                );
            }
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
                    new ExpressionStatement(new IfInstruction(condition.LdOutput(), ifTrueC, ifFalseC)))
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
            if (!this.Parameters.TryGetValue(pe.Id, out var v))
                throw new Exception($"Parameter {pe.Name}:{pe.Type} with id {pe.Id} is not defined");
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
            var value = this.TranslateExpression(e.Value);
            value = AdjustReference(value, wantReference: false, false);

            var to = this.Metadata.GetTypeReference(e.Type);

            var conversion = this.Metadata.CSharpConversions.ExplicitConversion(value.Output.Type, to);
            if (!conversion.IsValid)
                throw new Exception($"There isn't any valid conversion from {value.Output.Type} to {to}.");
            if (conversion.IsIdentityConversion)
                return value;
            if (!conversion.IsReferenceConversion && !conversion.IsBoxingConversion && !conversion.IsUnboxingConversion)
                throw new Exception($"There is not a reference conversion from {value.Output.Type} to {to}, but an '{conversion}' was found");

            var input = value.LdOutput();
            var instruction =
                conversion.IsBoxingConversion ? new Box(input, to) :
                conversion.IsUnboxingConversion ? new UnboxAny(input, to) :
                (ILInstruction)input;

            // reference conversions in IL code are simply omitted...
            return Result.Concat(
                value,
                Result.Expression(to, instruction)
            );
        }

        Result TranslateNumericConversion(NumericConversionExpression e)
        {
            var to = this.Metadata.GetTypeReference(e.Type);

            var targetPrimitive = to.GetDefinition().KnownTypeCode.ToPrimitiveType();
            if (targetPrimitive == PrimitiveType.None)
                throw new NotSupportedException($"Primitive type {to} is not supported.");

            var value = this.TranslateExpression(e.Value);

            var expr = new Conv(value.LdOutput(), targetPrimitive, e.Checked, value.Output.Type.GetSign());

            return Result.Concat(
                value,
                Result.Expression(to, expr)
            );
        }

        Result TranslateReferenceAssign(ReferenceAssignExpression e)
        {
            var target = this.TranslateExpression(e.Target);
            var value = this.TranslateExpression(e.Value);

            var type = Assert.IsType<TS.ByReferenceType>(target.Output.Type).ElementType;

            Assert.Equal(type, value.Output.Type);

            var load = new StObj(new LdLoc(target.Output), new LdLoc(value.Output), type);

            return Result.Concat(
                target,
                value,
                new Result(new ExpressionStatement(load))
            );
        }

        Result TranslateFieldAccess(FieldAccessExpression e)
        {
            if (e.Target is object)
                Assert.Equal(e.Field.DeclaringType(), e.Target.Type().UnwrapReference());
            //                                                        ^ auto-reference is allowed for targets

            var field = this.Metadata.GetField(e.Field);
            var target = e.Target?.Apply(TranslateExpression);
            target = AdjustReference(target, !(bool)field.DeclaringType.IsReferenceType, isReadonly: field.IsReadOnly);

            var load = ILAstFactory.FieldAddr(field, target?.Output);

            return Result.Concat(
                target ?? Result.Nop,
                Result.Expression(new TS.ByReferenceType(field.Type), load)
            );
        }

        Result TranslateArrayIndex(ArrayIndexExpression e)
        {
            var array = this.TranslateExpression(e.Array);
            var indices = e.Indices.Select(this.TranslateExpression).ToArray();
            var elementType = Assert.IsType<TS.ArrayType>(array.Output.Type).ElementType;
            var r = new IL.LdElema(
                elementType,
                array.LdOutput(),
                indices.Select(i => i.LdOutput()).ToArray()
            );
            return Result.Concat(
                array,
                Result.Concat(indices),
                Result.Expression(new TS.ByReferenceType(elementType), r)
            );
        }

        Result TranslateAddressOf(AddressOfExpression e)
        {
            var r = TranslateExpression(e.Expr);
            var addr = new AddressOf(r.LdOutput(), r.Output.Type);
            var type = new TS.ByReferenceType(r.Output.Type);
            return Result.Concat(
                r,
                Result.Expression(type, addr));
        }

        Result TranslateVariableReference(VariableReferenceExpression e)
        {
            if (!this.Parameters.TryGetValue(e.Variable.Id, out var v))
                throw new Exception($"Parameter {e.Variable.Name}:{e.Variable.Type} with id {e.Variable.Id} is not defined.");

            return Result.Expression(
                new TS.ByReferenceType(v.Type),
                new LdLoca(v));
        }

        Result TranslateDereference(DereferenceExpression e)
        {
            var r = TranslateExpression(e.Expr);
            var type = Assert.IsType<TS.ByReferenceType>(r.Output.Type).ElementType;
            return Result.Concat(
                r,
                Result.Expression(type, new LdObj(r.LdOutput(), type))
            );
        }

        Result TranslateNewObject(NewObjectExpression e)
        {
            var method = this.Metadata.GetMethod(e.Ctor);

            var args = e.Args.Select(this.TranslateExpression).ToArray();

            var call = new NewObj(method);
            call.Arguments.AddRange(args.Select(a => a.LdOutput()));

            return Result.Concat(args.Append(Result.Expression(method.DeclaringType, call)));
        }

        Result TranslateNewArray(NewArrayExpression e)
        {
            Assert.Equal(e.Type.Dimensions, e.Dimensions.Length);

            var elementType = this.Metadata.GetTypeReference(e.Type.Type);
            var indices = e.Dimensions.Select(TranslateExpression).ToArray();

            // TODO: validate indices

            var r = new IL.NewArr(elementType, indices.Select(i => i.LdOutput()).ToArray());

            return Result.Concat(
                Result.Concat(indices),
                Result.Expression(new TS.ArrayType(this.Metadata.Compilation, elementType, e.Type.Dimensions), r)
            );
        }

        static Result AdjustReference(ILVariable v, bool wantReference, bool isReadonly)
        {
            var type = v.Type is TS.ByReferenceType refType ? refType.ElementType : v.Type;

            if (wantReference)
            {
                if (v.StackType == StackType.Ref)
                    return new Result(v);
                else if (isReadonly)
                    // no need for special variable
                    return Result.Expression(new TS.ByReferenceType(type), new AddressOf(new LdLoc(v), type));
                else
                {
                    var tmpVar = new ILVariable(VariableKind.StackSlot, type);
                    return Result.Concat(
                        new Result(new ExpressionStatement(new LdLoc(v), tmpVar)),
                        Result.Expression(new TS.ByReferenceType(type), new LdLoca(tmpVar))
                    );
                }
            }
            else
            {
                if (v.StackType == StackType.Ref)
                    return Result.Expression(type, new LdObj(new LdLoc(v), type));
                else
                    return new Result(v);
            }
        }

        static Result AdjustReference(Result r, bool wantReference, bool isReadonly) =>
            r == null ? null :
            Result.Concat(
                r,
                AdjustReference(r.Output, wantReference, isReadonly)
            );

        static bool IsMethodReadonly(MethodReference m, IMethod m_)
        {
            if (m_.DeclaringType.GetDefinition()?.IsReadOnly == true)
                return true;
            if (m.Signature == PropertySignature.Nullable_HasValue.Getter ||
                m.Signature == PropertySignature.Nullable_Value.Getter)
                return true;

            return false;
        }

        Result TranslateMethodCall(MethodCallExpression e)
        {
            var signature = e.Method.Signature;
            Assert.Equal(signature.IsStatic, e.Target == null);
            // check types, no implicit conversions are allowed
            Assert.Equal(e.Method.Params().Select(p => p.Type), e.Args.Select(a => a.Type()));
            if (e.Target is object)
                Assert.Equal(e.Method.DeclaringType(), e.Target.Type().UnwrapReference());
            //                                                         ^ except auto-reference is allowed for targets

            var method = this.Metadata.GetMethod(e.Method);

            var args = e.Args.Select(TranslateExpression).ToArray();
            var target = e.Target?.Apply(TranslateExpression);
            target = AdjustReference(target, !(bool)method.DeclaringType.IsReferenceType, isReadonly: IsMethodReadonly(e.Method, method));

            var result = new List<Result>();
            target?.ApplyAction(result.Add);
            result.AddRange(args);

            var call = method.IsStatic ? new Call(method) : (CallInstruction)new CallVirt(method) { Arguments = { target.LdOutput() } };
            call.Arguments.AddRange(args.Select(a => a.LdOutput()));
            var isVoid = e.Method.Signature.ResultType == TypeSignature.Void;
            result.Add(isVoid ?
                       new Result(new ExpressionStatement(call)) :
                       Result.Expression(method.ReturnType, call));

            return Result.Concat(result);
        }

        Result TranslateBinary(BinaryExpression e)
        {
            var op = e.Operator;
            var type = e.Right.Type();
            Assert.Equal(e.Left.Type(), type);

            var left = TranslateExpression(e.Left);
            var right = TranslateExpression(e.Right);

            if (e.IsComparison())
            {
                var boolType = this.Metadata.Compilation.FindType(KnownTypeCode.Boolean);
                // primitive comparison
                if (op == "==" || op == "!=")
                {
                    if (left.Output.Type.IsReferenceType == false && left.Output.Type.GetStackType() == StackType.O)
                        throw new Exception($"Can not use '==' and '!=' operators for non-enum and non-primitive type {left.Output.Type}. If you wanted to call an overloaded operator, please the a method call expression.");

                    return Result.Concat(
                        left, right,
                        Result.Expression(boolType, new IL.Comp(
                            op == "==" ? ComparisonKind.Equality : ComparisonKind.Inequality,
                            Sign.None,
                            left.LdOutput(),
                            right.LdOutput()
                        ))
                    );
                }
                else
                {
                    var stackType = left.Output.Type.GetStackType();
                    if (stackType == StackType.O || stackType == StackType.Ref || stackType == StackType.Unknown)
                        throw new Exception($"Can not use comparison operators for non-integer type {left.Output.Type}. If you wanted to call an overloaded operator, please the a method call expression.");

                    return Result.Concat(
                        left, right,
                        Result.Expression(boolType, new IL.Comp(
                            op switch {
                                "<" => ComparisonKind.LessThan,
                                "<=" => ComparisonKind.LessThanOrEqual,
                                ">" => ComparisonKind.GreaterThan,
                                ">=" => ComparisonKind.GreaterThanOrEqual,
                                _ => throw new NotSupportedException($"Comparison operator {op} is not supported.")
                            },
                            left.Output.Type.GetSign(),
                            left.LdOutput(),
                            right.LdOutput()
                        ))
                    );
                }
            }
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

            public LdLoc LdOutput() => new LdLoc(this.Output);

            public static Result Nop = new Result();
            public bool IsNop => this.Statements.Count == 0 && Output is null;

            public static Result Expression(IType type, ILInstruction i)
            {
                var resultVar = new ILVariable(VariableKind.StackSlot, type);
                Assert.Equal(resultVar.StackType, i.ResultType);
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
