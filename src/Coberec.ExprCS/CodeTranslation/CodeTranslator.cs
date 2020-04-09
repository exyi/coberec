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
using IL = ICSharpCode.Decompiler.IL;
using TS = ICSharpCode.Decompiler.TypeSystem;

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
            var declaringType = method.Signature.DeclaringType.SpecializeByItself();
            if (!method.Signature.IsStatic)
            {
                var firstArgument = method.ArgumentParams.First();
                if (declaringType.Type.IsValueType)
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
            if (r.IsVoid && r.Statements.Count == 1 && r.Statements[0] is ExpressionStatement expr)
            {
                Assert.Null(expr.Output);
                Assert.Null(resultVar);
                return expr.Instruction;
            }

            if (r.Statements.Count == 0)
            {
                if (r.IsVoid)
                    return new Nop();
                return new StLoc(resultVar, r.Instr());
            }

            return this.BuildBContainer(r.WithOutputInto(resultVar));
        }

        BlockContainer BuildBContainer(Result r)
        {
            var isVoid = r.IsVoid;
            var container = new IL.BlockContainer(expectedResultType: isVoid ? IL.StackType.Void : r.Type.GetStackType());
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
                else        block.Instructions.Add(new IL.Leave(container, value: r.Instr()));
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
                e => TranslateBinary(e),
                e => TranslateNot(e),
                e => TranslateMethodCall(e),
                e => TranslateNewObject(e),
                e => TranslateFieldAccess(e),
                e => TranslateReferenceAssign(e),
                e => TranslateDereference(e),
                e => TranslateVariableReference(e),
                e => TranslateAddressOf(e),
                e => TranslateNumericConversion(e),
                e => TranslateReferenceConversion(e),
                e => TranslateConstant(e),
                e => TranslateDefault(e),
                e => TranslateParameter(e),
                e => TranslateConditional(e),
                e => TranslateFunction(e),
                e => TranslateFunctionConversion(e),
                e => TranslateInvoke(e),
                e => TranslateBreak(e),
                e => TranslateBreakable(e),
                e => TranslateLoop(e),
                e => TranslateLetIn(e),
                e => TranslateNewArray(e),
                e => TranslateArrayIndex(e),
                e => TranslateBlock(e),
                e => TranslateLowerable(e));
            var expectedType = expr.Type();
            if (expectedType == TypeSignature.Void)
                Assert.True(result.IsVoid);
            else
                Assert.False(result.IsVoid);
            if (expectedType is TypeReference.FunctionTypeCase fn)
            {
                Assert.Equal(StackType.O, result.Type.GetStackType());
                var invokeMethod = result.Type.GetDelegateInvokeMethod();
                Assert.NotNull(invokeMethod);
                Assert.Equal(invokeMethod.Parameters.Count, fn.Item.Params.Length);
                Assert.Equal(invokeMethod.Parameters.Select(p => SymbolLoader.TypeRef(p.Type)), fn.Item.Params.Select(p => p.Type));
                Assert.Equal(SymbolLoader.TypeRef(invokeMethod.ReturnType), fn.Item.ResultType);
            }

            else if (expectedType != TypeSignature.Void)
            {
                var t = this.Metadata.GetTypeReference(expectedType);
                Assert.Equal(t.FullName, result.Type.FullName);
            }

            return result;
        }

        
        Result TranslateNot(NotExpression item)
        {
            var r = this.TranslateExpression(item.Expr);
            Assert.False(r.IsVoid);
            var expr = Comp.LogicNot(r.Instr());
            return Result.Concat(
                r,
                Result.Expression(r.Type, expr)
            );
        }

        Result TranslateLowerable(LowerableExpression e) =>
            this.TranslateExpression(e.Lowered);

        Result TranslateBlock(BlockExpression block) =>
            block.Expressions
            .Select(e => TranslateExpression(e).AsVoid())
            .ToArray()
            .Append(TranslateExpression(block.Result))
            .Apply(Result.Concat);

        Result TranslateLetIn(LetInExpression e)
        {
            if (e.Value is Expression.FunctionCase fn)
                return TranslateLocalFunction(fn.Item, e.Variable, e.Target);

            var value = this.TranslateExpression(e.Value);

            Assert.False(value.IsVoid);
            Assert.NotEqual(TypeSignature.Void, e.Variable.Type); // TODO: add support for voids?
            Assert.Equal(value.Type, this.Metadata.GetTypeReference(e.Variable.Type));

            var ilVar = new ILVariable(VariableKind.Local, value.Type);
            ilVar.Name = e.Variable.Name;
            this.Parameters.Add(e.Variable.Id, ilVar);
            var target = this.TranslateExpression(e.Target);
            this.Parameters.Remove(e.Variable.Id);
            return Result.Concat(
                value,
                new Result(new ExpressionStatement(new StLoc(ilVar, value.Instr()))),
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
                    expr.AsVoid(),
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
                    expr.AsVoid(),
                    new Result(
                        resultVariable?.Type,
                        resultVariable is object ? new LdLoc(resultVariable) : null,
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
                Assert.True(value.IsVoid);
                return Result.Concat(
                    value,
                    new Result(ImmutableList.Create(breakStmt))
                );
            }
            else
            {
                var value = this.TranslateExpression(e.Value);

                Assert.False(value.IsVoid);
                Assert.NotNull(resultVar);

                return Result.Concat(
                    value,
                    new Result(ImmutableList.Create(
                        new ExpressionStatement(output: resultVar, instruction: value.Instr()),
                        breakStmt
                    ))
                );
            }
        }

        Result TranslateConditional(ConditionalExpression item)
        {
            // TODO: shortcut for simplest expressions
            Assert.Equal(item.IfTrue.Type(), item.IfFalse.Type());

            if (item.Condition is Expression.ConstantCase constant) // TODO: optimization phase
                return TranslateExpression((bool)constant.Item.Value ? item.IfTrue : item.IfFalse);

            var condition = this.TranslateExpression(item.Condition);
            Assert.False(condition.IsVoid);
            var ifTrue = this.TranslateExpression(item.IfTrue);
            var ifFalse = this.TranslateExpression(item.IfFalse);

            if (ifTrue.Statements.IsEmpty && ifFalse.Statements.IsEmpty && !ifTrue.IsVoid)
            {
                // shortcut for simple expressions
                return Result.Concat(
                    condition,
                    new Result(
                        ifTrue.Type,
                        new IfInstruction(condition.Instr(), ifTrue.Instr(), ifFalse.Instr())
                    )
                );
            }

            var resultVar = this.CreateOutputVar(item.IfTrue.Type());

            var ifTrueC = this.ToILInstruction(ifTrue, resultVar);
            var ifFalseC = ifFalse.IsNop ? null : this.ToILInstruction(ifFalse, resultVar);

            return Result.Concat(
                condition,
                new Result(
                    resultVar?.Type,
                    resultVar is object ? new LdLoc(resultVar) : null,
                    new ExpressionStatement(new IfInstruction(condition.Instr(), ifTrueC, ifFalseC)))
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
            return new Result(v.Type, new LdLoc(v));
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

            var conversion = this.Metadata.CSharpConversions.ExplicitConversion(value.Type, to);
            if (!conversion.IsValid)
                throw new Exception($"There isn't any valid conversion from {value.Type} to {to}.");
            if (conversion.IsIdentityConversion)
                return value;
            if (!conversion.IsReferenceConversion && !conversion.IsBoxingConversion && !conversion.IsUnboxingConversion)
                throw new Exception($"There is not a reference conversion from {value.Type} to {to}, but an '{conversion}' was found");

            var input = value.Instr();
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

            var expr = new Conv(value.Instr(), targetPrimitive, e.Checked, value.Type.GetSign());

            return Result.Concat(
                value,
                Result.Expression(to, expr)
            );
        }

        Result TranslateReferenceAssign(ReferenceAssignExpression e)
        {
            var target = this.TranslateExpression(e.Target);
            var value = this.TranslateExpression(e.Value);

            var (args_r, args) = Result.CombineInstr(target, value);

            var type = Assert.IsType<TS.ByReferenceType>(target.Type).ElementType;

            Assert.Equal(type.WithoutNullability(), value.Type.WithoutNullability());

            var load = new StObj(args[0], args[1], type);

            return Result.Concat(
                args_r,
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

            var load = ILAstFactory.FieldAddr(field, target?.Instr());

            return Result.Concat(
                target ?? Result.Nop,
                Result.Expression(new TS.ByReferenceType(field.Type), load)
            );
        }

        Result TranslateArrayIndex(ArrayIndexExpression e)
        {
            var array = this.TranslateExpression(e.Array);
            var indices = e.Indices.Select(this.TranslateExpression).ToArray();
            var elementType = Assert.IsType<TS.ArrayType>(array.Type).ElementType;

            var (args_r, args) = Result.CombineInstr(indices.Prepend(array));

            var r = new IL.LdElema(
                elementType,
                args[0],
                args.Skip(1).ToArray()
            );
            return Result.Concat(
                args_r,
                Result.Expression(new TS.ByReferenceType(elementType), r)
            );
        }

        Result TranslateAddressOf(AddressOfExpression e)
        {
            var r = TranslateExpression(e.Expr);
            var addr = new AddressOf(r.Instr(), r.Type);
            var type = new TS.ByReferenceType(r.Type);
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
            var type = Assert.IsType<TS.ByReferenceType>(r.Type).ElementType;
            return Result.Concat(
                r,
                Result.Expression(type, new LdObj(r.Instr(), type))
            );
        }

        Result TranslateNewObject(NewObjectExpression e)
        {
            var method = this.Metadata.GetMethod(e.Ctor);

            var args_raw = e.Args.Select(this.TranslateExpression).ToArray();

            var (args_r, args) = Result.CombineInstr(args_raw);


            var call = new NewObj(method);
            call.Arguments.AddRange(args);

            return Result.Concat(args_r, Result.Expression(method.DeclaringType, call));
        }

        Result TranslateNewArray(NewArrayExpression e)
        {
            Assert.Equal(e.Type.Dimensions, e.Dimensions.Length);

            var elementType = this.Metadata.GetTypeReference(e.Type.Type);
            var indices_raw = e.Dimensions.Select(TranslateExpression).ToArray();

            var (indices_r, indices) = Result.CombineInstr(indices_raw);

            // TODO: validate indices

            var r = new IL.NewArr(elementType, indices.ToArray());

            return Result.Concat(
                indices_r,
                Result.Expression(new TS.ArrayType(this.Metadata.Compilation, elementType, e.Type.Dimensions), r)
            );
        }

        static Result AdjustReference(ILInstruction v, IType instrType, bool wantReference, bool isReadonly)
        {
            var type = instrType is TS.ByReferenceType refType ? refType.ElementType : instrType;

            if (wantReference)
            {
                if (v.ResultType == StackType.Ref)
                    return Result.Expression(instrType, v);
                else if (isReadonly)
                    // no need for special variable
                    return Result.Expression(new TS.ByReferenceType(type), new AddressOf(v, type));
                else
                {
                    var tmpVar = new ILVariable(VariableKind.StackSlot, type);
                    return Result.Concat(
                        new Result(new ExpressionStatement(v, tmpVar)),
                        Result.Expression(new TS.ByReferenceType(type), new LdLoca(tmpVar))
                    );
                }
            }
            else
            {
                if (v.ResultType == StackType.Ref)
                    return Result.Expression(type, new LdObj(v, type));
                else
                    return Result.Expression(type, v);
            }
        }

        static Result AdjustReference(Result r, bool wantReference, bool isReadonly) =>
            r == null ? null :
            Result.Concat(
                r,
                AdjustReference(r.Instr(), r.Type, wantReference, isReadonly)
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

            var args_raw = e.Args.Select(TranslateExpression).ToList();
            var target = e.Target?.Apply(TranslateExpression);
            target = AdjustReference(target, !(bool)method.DeclaringType.IsReferenceType, isReadonly: IsMethodReadonly(e.Method, method));

            if (target is object) args_raw.Insert(0, target);
            var (args_r, args) = Result.CombineInstr(args_raw);


            var call = method.IsStatic ? new Call(method) : (CallInstruction)new CallVirt(method);
            call.Arguments.AddRange(args);
            var isVoid = e.Method.Signature.ResultType == TypeSignature.Void;

            return Result.Concat(
                args_r,
                isVoid ? new Result(new ExpressionStatement(call))
                       : Result.Expression(method.ReturnType, call)
            );
        }

        Result TranslateBinary(BinaryExpression e)
        {
            var op = e.Operator;

            // error messages for known C# operators that should not be used like this
            var err = op switch {
                "&&" => "Can't use `&&` operator in BinaryExpression, use Expression.And(a, b) instead.",
                "||" => "Can't use `||` operator in BinaryExpression, use Expression.Or(a, b) instead.",
                "??" => "Can't use `??` operator in BinaryExpression, use a.NullCoalesce(b) instead.",
                var x when x.EndsWith("=") => $"Can't use operation+assignment `{x}` in binary expression. Split it into two operations.",
                _ => null
            };
            if (err is object) throw new NotSupportedException(err);

            var type = e.Right.Type();
            Assert.Equal(e.Left.Type(), type);

            var left_raw = TranslateExpression(e.Left);
            var right_raw = TranslateExpression(e.Right);

            var (args_r, args) = Result.CombineInstr(left_raw, right_raw);
            var left = args[0];
            var right = args[1];

            Assert.Equal(left.ResultType, left_raw.Type.GetStackType());
            Assert.Equal(right.ResultType, right_raw.Type.GetStackType());

            if (e.IsComparison())
            {
                var boolType = this.Metadata.Compilation.FindType(KnownTypeCode.Boolean);
                // primitive comparison
                if (op == "==" || op == "!=")
                {
                    if (left_raw.Type.IsReferenceType == false && left_raw.Type.GetStackType() == StackType.O)
                        throw new Exception($"Cannot not use '==' and '!=' operators for non-enum and non-primitive type {left_raw.Type}. If you wanted to call an overloaded operator, please use the a method call expression.");

                    return Result.Concat(
                        args_r,
                        Result.Expression(boolType, new IL.Comp(
                            op == "==" ? ComparisonKind.Equality : ComparisonKind.Inequality,
                            Sign.None,
                            left,
                            right
                        ))
                    );
                }
                else
                {
                    var stackType = left_raw.Type.GetStackType();
                    if (stackType == StackType.O || stackType == StackType.Ref || stackType == StackType.Unknown)
                        throw new Exception($"Cannot not use comparison operators for non-integer type {left_raw.Type}. If you wanted to call an overloaded operator, please use the a method call expression.");

                    return Result.Concat(
                        args_r,
                        Result.Expression(boolType, new IL.Comp(
                            op switch {
                                "<" => ComparisonKind.LessThan,
                                "<=" => ComparisonKind.LessThanOrEqual,
                                ">" => ComparisonKind.GreaterThan,
                                ">=" => ComparisonKind.GreaterThanOrEqual,
                                _ => throw new NotSupportedException($"Comparison operator {op} is not supported.")
                            },
                            left_raw.Type.GetSign(),
                            left,
                            right
                        ))
                    );
                }
            }
            else
            {
                var stackType = left_raw.Type.GetStackType();
                if (stackType == StackType.O || stackType == StackType.Ref || stackType == StackType.Unknown)
                    throw new Exception($"Cannot use arithmentic operators for non-integer type {left_raw.Type}. If you wanted to call an overloaded operator, please use the a method call expression.");

                Assert.Equal(left_raw.Type, right_raw.Type);

                return Result.Concat(
                    args_r,
                    Result.Expression(left_raw.Type, new IL.BinaryNumericInstruction(
                        op switch {
                            "+" => BinaryNumericOperator.Add,
                            "&" => BinaryNumericOperator.BitAnd,
                            "|" => BinaryNumericOperator.BitOr,
                            "^" => BinaryNumericOperator.BitXor,
                            "/" => BinaryNumericOperator.Div,
                            "*" => BinaryNumericOperator.Mul,
                            "%" => BinaryNumericOperator.Rem,
                            "<<" => BinaryNumericOperator.ShiftLeft,
                            ">>" => BinaryNumericOperator.ShiftRight,
                            "-" => BinaryNumericOperator.Sub,
                            _ => throw new NotSupportedException($"Numeric operator {op} is not supported.")
                        },
                        left,
                        right,
                        checkForOverflow: false,
                        left_raw.Type.GetSign()
                    ))
                );
            }
        }

        Result TranslateConstant(ConstantExpression e)
        {
            var type = this.Metadata.GetTypeReference(e.Type);
            return Result.Expression(type, ILAstFactory.Constant(e.Value, type));
        }
    }
}
