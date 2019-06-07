using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Coberec.CSharpGen;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;
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
            var statements = TranslateExpression(method.Body, methodCx);
            return BuildTheFunction(statements, methodCx);
        }

        static ILFunction BuildTheFunction(Result r, MethodContext cx)
        {
            var isVoid = cx.GeneratedMethod.ReturnType.FullName == "System.Void";

            Assert.Equal(isVoid, (r.Output == null));

            var instructions = r.Statements.Cast<ExpressionStatement>()
                               .Where(e => e.Instruction != null) // ignore variable reads
                               .Select(e => e.Output == null ? e.Instruction : new StLoc(e.Output, e.Instruction))
                               .ToArray();

            var functionContainer = new IL.BlockContainer(expectedResultType: isVoid ? IL.StackType.Void : r.Output.StackType);
            var block = new IL.Block();
            var variables = new VariableCollectingVisitor();
            r.Output?.Apply(variables.Variables.Add);
            foreach (var i in instructions)
            {
                i.AcceptVisitor(variables);
                block.Instructions.Add(i);
            }
            if (isVoid) block.Instructions.Add(new IL.Leave(functionContainer));
            else block.Instructions.Add(new IL.Leave(functionContainer, value: new LdLoc(r.Output)));

            functionContainer.Blocks.Add(block);

            var ilFunc = new IL.ILFunction(cx.GeneratedMethod, 10000, new ICSharpCode.Decompiler.TypeSystem.GenericContext(), functionContainer);

            foreach (var i in variables.Variables)
            {
                if (i.Function == null)
                    ilFunc.Variables.Add(i);
            }

            ilFunc.AddRef(); // whatever, somehow initializes the freaking tree
            ilFunc.CheckInvariantPublic(IL.ILPhase.Normal);
            return ilFunc;
        }

        class VariableCollectingVisitor : IL.ILVisitor
        {
            public readonly HashSet<IL.ILVariable> Variables = new HashSet<IL.ILVariable>();

            protected override void Default(IL.ILInstruction inst)
            {
                if (inst is IL.IInstructionWithVariableOperand lfslfd)
                    Variables.Add(lfslfd.Variable);

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

            Assert.NotNull(value.Output != null);
            Assert.Equal(value.Output.Type, cx.Metadata.GetTypeReference(e.Variable.Type));

            var ilVar = new ILVariable(VariableKind.Local, value.Output.Type);
            ilVar.Name = e.Variable.Name;
            cx.Parameters.Add(e.Variable.Id, ilVar);
            var target = TranslateExpression(e.Target, cx);
            cx.Parameters.Remove(e.Variable.Id);
            return Result.Concat(
                value,
                new Result(ImmutableList.Create<Statement>(new ExpressionStatement { Instruction = new StLoc(ilVar, new LdLoc(value.Output)) })),
                target);
        }

        private static Result TranslateLoop(LoopExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateBreakable(BreakableExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateBreak(BreakExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        private static Result TranslateParameter(ParameterExpression pe, MethodContext cx)
        {
            var v = cx.Parameters[pe.Id];
            return new Result(ImmutableList<Statement>.Empty, v);
        }

        private static Result TranslateDefault(DefaultExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
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

        private static Result TranslateMethodCall(MethodCallExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateBinary(BinaryExpression item, MethodContext cx)
        {
            throw new NotImplementedException();
        }

        private static Result TranslateConstant(ConstantExpression e, MethodContext cx)
        {
            var type = cx.Metadata.GetTypeReference(e.Type);
            var outVar = new ILVariable(VariableKind.StackSlot, type);
            return new Result(
                statements: ImmutableList.Create<Statement>(
                    new ExpressionStatement {
                        Output = outVar,
                        Instruction = TranslateConstant(e.Value, type)
                    }),
                output: outVar
            );
        }

        private static ILInstruction TranslateConstant(object constant, IType type)
        {
            if (constant == null)
            {
                if (type.IsReferenceType == true) return new LdNull();
                else return new IL.DefaultValue(type);
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
        }



        class Statement
        {
        }

        class ExpressionStatement : Statement
        {
            public ILVariable Output;
            public ILInstruction Instruction;
        }

        class Result
        {
            public readonly ImmutableList<Statement> Statements;
            public readonly ILVariable Output;

            public Result(IEnumerable<Statement> statements, ILVariable output = null)
            {
                this.Statements = statements.ToImmutableList();
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
                return new Result(s, output);
            }
        }
    }
}
