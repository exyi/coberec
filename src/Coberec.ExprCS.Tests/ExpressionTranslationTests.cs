using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests
{
    static class Helpers
    {
        public static void AddTestExpr(this MetadataContext cx, Expression expr, params ParameterExpression[] parameters)
        {
            var typeCount = cx.DefinedTypes.Count + cx.WaitingTypes.Count();
            var name = typeCount < 15 ? ((char)('C' + typeCount)).ToString() : "C" + typeCount;
            var ns = ((TypeOrNamespace.NamespaceSignatureCase)cx.DefinedTypes.FirstOrDefault()?.Signature.Parent)?.Item ??
                    new NamespaceSignature("NS", NamespaceSignature.Global);
            var type = TypeSignature.Class(name, ns, Accessibility.APublic);
            var method = new MethodSignature(type, parameters.Select(p => new MethodParameter(p.Type, p.Name)).ToImmutableArray(), "M", expr.Type(), true, Accessibility.APublic, false, false, false, false, ImmutableArray<GenericParameter>.Empty);
            var methodDef = new MethodDef(method, parameters.ToImmutableArray(), expr);
            var typeDef = TypeDef.Empty(type).With(members: ImmutableArray.Create<MemberDef>(methodDef));

            cx.AddType(typeDef);
            cx.CommitWaitingTypes();
        }
    }

    public class ExpressionTranslationTests
    {
        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create("MyModule");
        ParameterExpression p1 = ParameterExpression.Create(TypeSignature.Int32, "p1");
        ParameterExpression p2 = ParameterExpression.Create(TypeSignature.Int32, "p2");
        ParameterExpression pBool1 = ParameterExpression.Create(TypeSignature.Boolean, "pBool1");
        ParameterExpression pString1 = ParameterExpression.Create(TypeSignature.String, "pString1");
        ParameterExpression pObject = ParameterExpression.Create(TypeSignature.Object, "pObject");
        ParameterExpression pTime = ParameterExpression.Create(TypeSignature.TimeSpan, "pTime");

        public ExpressionTranslationTests()
        {
            RaiseExceptionDebugProvider.HackIt();
        }


        [Fact]
        public void ArgumentPassing()
        {
            cx.AddTestExpr(p1, p1);
            check.CheckOutput(cx);
        }

        [Fact]
        public void VariableDeclaration()
        {
            cx.AddTestExpr(Expression.LetIn(p2, p1, p2), p1);
            check.CheckOutput(cx);
        }

        [Fact]
        public void MethodInvocation()
        {
            var intParse = cx.GetMemberMethods(TypeSignature.Int32.NotGeneric()).Single(m => m.Name() == "Parse" && m.Signature.Params.Length == 1 && m.Signature.Params[0].Type == TypeSignature.String);
            var stringConcat = cx.GetMemberMethods(TypeSignature.String.NotGeneric()).Single(m => m.Name() == "Concat" && m.Signature.Params.Length == 2 && m.Signature.Params[0].Type == TypeSignature.String);

            var concatCall = Expression.MethodCall(stringConcat, ImmutableArray.Create(Expression.Constant("123456789"), pString1), null);
            var intMethod = Expression.MethodCall(intParse, ImmutableArray.Create(concatCall), null);
            var voidMethod = Expression.Block(ImmutableArray.Create(intMethod), Expression.Nop);

            cx.AddTestExpr(intMethod, pString1);
            cx.AddTestExpr(Expression.LetIn(pString1, Expression.Constant("5"), intMethod));
            cx.AddTestExpr(voidMethod, pString1);
            cx.AddTestExpr(Expression.Block(ImmutableArray.Create(concatCall), Expression.Nop), pString1);

            check.CheckOutput(cx);
        }

        [Fact]
        public void SimplestIfCondition()
        {
            var e = Expression.Conditional(pBool1, p1, p2);
            cx.AddTestExpr(e, pBool1, p1, p2);
            check.CheckOutput(cx);
        }

        [Fact]
        public void InfiniteLoop()
        {
            var call = ExampleMethodCall(cx);

            var loop = Expression.Loop(call);

            cx.AddTestExpr(loop);
            check.CheckOutput(cx);
        }

        static Expression ExampleMethodCall(MetadataContext cx)
        {
            var intParse = cx.GetMemberMethods(TypeSignature.Int32.NotGeneric()).Single(m => m.Signature.Name == "Parse" && m.Signature.Params.Length == 1 && m.Signature.Params[0].Type == TypeSignature.String);
            return Expression.MethodCall(intParse, ImmutableArray.Create(Expression.Constant("123456789")), null);
        }

        static Expression MakeExampleBreak(MetadataContext cx, LabelTarget label)
        {
            var call = ExampleMethodCall(cx);
            var condition = ExampleCondition(cx);

            var ifBlock = Expression.IfThen(
                condition,
                Expression.Break(Expression.Nop, label));

            return Expression.Block(ImmutableArray.Create<Expression>(
                call,
                ifBlock,
                call
            ), Expression.Nop);
        }

        static Expression ExampleCondition(MetadataContext cx)
        {
            var thread = TypeSignature.FromType(typeof(System.Threading.Thread)).NotGeneric();
            var currentThread = cx.GetMemberProperty(thread, "CurrentThread").Getter();
            var isBackground = cx.GetMemberProperty(((TypeReference.SpecializedTypeCase)currentThread.ResultType()).Item, "IsBackground").Getter();
            var condition = Expression.MethodCall(isBackground, ImmutableArray<Expression>.Empty, Expression.MethodCall(currentThread, ImmutableArray<Expression>.Empty, null));
            return condition;
        }

        [Fact]
        public void Breakable()
        {
            LabelTarget label = LabelTarget.New("l");
            Expression @break = MakeExampleBreak(cx, label);

            var b = Expression.Breakable(@break, label);
            cx.AddTestExpr(b);
            check.CheckOutput(cx);
        }

        [Fact]
        public void BreakableInsideBlock()
        {
            LabelTarget label = LabelTarget.New("l");
            Expression @break = MakeExampleBreak(cx, label);

            var bb = Expression.Breakable(@break, label);
            var call = ExampleMethodCall(cx);

            var block = Expression.Block(ImmutableArray.Create<Expression>(
                bb,
                call
            ), Expression.Constant(12));
            cx.AddTestExpr(block);
            check.CheckOutput(cx);
        }

        [Fact]
        public void WhileLoop()
        {
            var e = Expression.While(ExampleCondition(cx), ExampleMethodCall(cx));
            cx.AddTestExpr(e);
            check.CheckOutput(cx);
        }

        MethodReference toStringMethod => cx.GetMemberMethods(TypeSignature.Object.NotGeneric()).Single(m => m.Signature.Name == "ToString");

        [Fact]
        public void ValueBoxing()
        {
            var e = Expression.ReferenceConversion(Expression.Constant(1), TypeSignature.Object);
            cx.AddTestExpr(e);

            cx.AddTestExpr(Expression.ReferenceConversion(pTime, TypeSignature.Object), pTime);

            check.CheckOutput(cx);
        }

        [Fact]
        public void ValueUnboxing()
        {
            cx.AddTestExpr(Expression.ReferenceConversion(pObject, TypeSignature.TimeSpan), pObject);
            cx.AddTestExpr(Expression.ReferenceConversion(pObject, TypeSignature.Int32), pObject);

            check.CheckOutput(cx);
        }

        [Fact]
        public void ReferenceCast()
        {
            cx.AddTestExpr(Expression.ReferenceConversion(pObject, TypeSignature.String), pObject);
            cx.AddTestExpr(Expression.ReferenceConversion(pString1, TypeSignature.Object), pString1);

            check.CheckOutput(cx);
        }

        [Fact]
        public void LocalFunction()
        {
            var fn1 = Expression.Function(Expression.Constant(1));
            var v1 = ParameterExpression.Create(fn1.Type(), "v1");
            var fn2 = Expression.Function(
                        Expression.Conditional(
                            pBool1,
                            Expression.ReferenceConversion(Expression.Invoke(v1, ImmutableArray<Expression>.Empty), TypeSignature.Object),
                            Expression.Constant<object>(null)
                        ),
                        pBool1);
            var v2 = ParameterExpression.Create(fn2.Type(), "v2");

            cx.AddTestExpr(
                Expression.LetIn(v1, fn1, Expression.LetIn(v2, fn2,
                    Expression.Invoke(v2, ImmutableArray.Create(Expression.Constant(true)))
                )));
            check.CheckOutput(cx);
        }

        [Fact]
        public void LambdaFunction()
        {
            var fn1 = Expression.Function(Expression.Constant(1));
            var fn2 = Expression.Function(
                        Expression.Conditional(
                            pBool1,
                            Expression.ReferenceConversion(Expression.Invoke(fn1, ImmutableArray<Expression>.Empty), TypeSignature.Object),
                            Expression.Constant<object>(null)
                        ),
                        pBool1);

            cx.AddTestExpr(
                Expression.Invoke(fn2, ImmutableArray.Create(Expression.Constant(true)))
            );
            check.CheckOutput(cx);
        }

        [Fact]
        public void ReturnLambdaFunction()
        {
            var fn1 = Expression.Function(Expression.Constant(1));
            var fn2 = Expression.Function(
                        Expression.Conditional(
                            pBool1,
                            Expression.ReferenceConversion(Expression.Invoke(fn1, ImmutableArray<Expression>.Empty), TypeSignature.Object),
                            Expression.Constant<object>(null)
                        ),
                        pBool1);

            cx.AddTestExpr(Expression.FunctionConversion(fn1, new FunctionType(ImmutableArray<MethodParameter>.Empty, TypeSignature.Int32).TryGetDelegate()));
            cx.AddTestExpr(Expression.FunctionConversion(Expression.Function(Expression.Constant("abc")), new FunctionType(ImmutableArray<MethodParameter>.Empty, TypeSignature.Object).TryGetDelegate()));
            cx.AddTestExpr(Expression.FunctionConversion(fn2, new FunctionType(ImmutableArray.Create(new MethodParameter(TypeSignature.Boolean, "a")), TypeSignature.Object).TryGetDelegate()));
            check.CheckOutput(cx);
        }

        [Fact]
        public void TrivialFunctionConversions()
        {
            var stringFunc = new FunctionType(ImmutableArray<MethodParameter>.Empty, TypeSignature.String).TryGetDelegate();
            var objectFunc = new FunctionType(ImmutableArray<MethodParameter>.Empty, TypeSignature.Object).TryGetDelegate();
            var pStringFunc = ParameterExpression.Create(stringFunc, "stringFunc");
            cx.AddTestExpr(Expression.ReferenceConversion(pStringFunc, objectFunc), pStringFunc);
            cx.AddTestExpr(Expression.FunctionConversion(pStringFunc, objectFunc), pStringFunc);
            cx.AddTestExpr(Expression.FunctionConversion(Expression.FunctionConversion(pStringFunc, new FunctionType(ImmutableArray<MethodParameter>.Empty, TypeSignature.String)), stringFunc), pStringFunc);
            cx.AddTestExpr(Expression.FunctionConversion(Expression.FunctionConversion(pStringFunc, new FunctionType(ImmutableArray<MethodParameter>.Empty, TypeSignature.Object)), objectFunc), pStringFunc);

            // var predicate = cx.FindType(typeof(Predicate<string>));
            // var predicateP = ParameterExpression.Create(predicate, "predicateP");
            // cx.AddTestExpr(Expression.FunctionConversion(predicateP, cx.FindType(typeof(Func<string, bool>))), predicateP);
            check.CheckOutput(cx);
        }

        [Fact]
        public void TransdelegateFunctionConversion()
        {
            var predicate = TypeReference.FromType(typeof(Predicate<string>));
            var predicateP = ParameterExpression.Create(predicate, "predicateP");
            cx.AddTestExpr(Expression.FunctionConversion(predicateP, TypeReference.FromType(typeof(Func<string, bool>))), predicateP);
            check.CheckOutput(cx);
        }


        [Fact]
        public void ConstructorCall()
        {
            cx.AddTestExpr(Expression.NewObject(
                MethodReference.FromLambda(() => new List<String>()),
                ImmutableArray<Expression>.Empty
            ));

            cx.AddTestExpr(Expression.NewObject(
                MethodReference.FromLambda(() => new List<String>(55)),
                ImmutableArray.Create(Expression.Constant(1234))
            ));

            cx.AddTestExpr(Expression.NewObject(
                MethodReference.FromLambda(() => new DateTime(55L)),
                ImmutableArray.Create(Expression.Constant(1234L))
            ));

            check.CheckOutput(cx);
        }

        [Fact]
        public void ArrayConstructorCall()
        {
            cx.AddTestExpr(Expression.NewArray(
                new ArrayType(TypeSignature.Int32, dimensions: 1),
                Expression.Constant(12)
            ));

            check.CheckOutput(cx);
        }

        [Fact]
        public void MultiDimArrayConstructorCall()
        {
            cx.AddTestExpr(Expression.NewArray(
                new ArrayType(TypeSignature.String, 3),
                Expression.Constant(11),
                Expression.Constant(12),
                Expression.Constant(13)
            ));

            check.CheckOutput(cx);
        }

        [Fact]
        public void ArrayOfArraysConstructorCall()
        {
            cx.AddTestExpr(Expression.NewArray(
                new ArrayType(new ArrayType(TypeSignature.IEnumerableOfT.Specialize(TypeSignature.Int32), 1), 1),
                Expression.Constant(12)
            ));

            cx.AddTestExpr(Expression.NewArray(
                new ArrayType(new ArrayType(TypeSignature.String, 5), 3),
                Expression.Constant(11),
                Expression.Constant(12),
                Expression.Constant(13)
            ));

            check.CheckOutput(cx);
        }

        [Fact]
        public void ArrayIndex()
        {
            var arrayP = ParameterExpression.Create(new ArrayType(TypeSignature.String, 1), "a");
            cx.AddTestExpr(Expression.ArrayIndex(
                arrayP,
                Expression.Constant(11)
            ).Dereference(), arrayP);

            cx.AddTestExpr(Expression.ArrayIndex(
                arrayP,
                Expression.Constant(11)
            ).ReferenceAssign(Expression.Constant("abc")), arrayP);

            check.CheckOutput(cx);
        }

        [Fact]
        public void MultidimArrayIndex()
        {
            var arrayP = ParameterExpression.Create(new ArrayType(TypeSignature.String, 3), "a");
            cx.AddTestExpr(Expression.ArrayIndex(
                arrayP,
                Expression.Constant(11),
                Expression.Constant(12),
                Expression.Constant(13)
            ).Dereference(), arrayP);

            cx.AddTestExpr(Expression.ArrayIndex(
                arrayP,
                Expression.Constant(11),
                Expression.Constant(12),
                Expression.Constant(13)
            ).ReferenceAssign(Expression.Constant("abc")), arrayP);

            check.CheckOutput(cx);
        }

        [Fact]
        public void RefReturn()
        {
            var arrayP = ParameterExpression.Create(new ArrayType(TypeSignature.String, 1), "a");
            cx.AddTestExpr(Expression.ArrayIndex(
                arrayP,
                Expression.Constant(11)
            ), arrayP);

            var marrayP = ParameterExpression.Create(new ArrayType(TypeSignature.String, 3), "a");
            cx.AddTestExpr(Expression.ArrayIndex(
                marrayP,
                Expression.Constant(11),
                Expression.Constant(12),
                Expression.Constant(13)
            ), marrayP);

            var refP = ParameterExpression.Create(new ByReferenceType(TypeSignature.Int32), "r");
            cx.AddTestExpr(refP, refP);


            var myTupleP = ParameterExpression.Create(new ByReferenceType(TypeReference.FromType(typeof((int, int)))), "myTuple");

            cx.AddTestExpr(Expression.FieldAccess(FieldReference.FromLambda<(int, int)>(r => r.Item1), target: myTupleP), myTupleP);

            check.CheckOutput(cx);
        }

        [Fact(Skip = "This does not work")]
        public void RefReturnCondition()
        {
            var refP1 = ParameterExpression.Create(new ByReferenceType(TypeSignature.Int32), "r1");
            var refP2 = ParameterExpression.Create(new ByReferenceType(TypeSignature.Int32), "r2");
            cx.AddTestExpr(refP1, refP1);
            cx.AddTestExpr(Expression.Conditional(pBool1, refP1, refP2), refP1, refP2, pBool1);
        }

        [Fact]
        public void StaticFieldAccess()
        {
            var field1 = FieldReference.FromLambda<object>(_ => DateTime.MinValue);
            var field2 = FieldReference.FromLambda<object>(_ => System.Reflection.Emit.OpCodes.Call);
            var field3 = FieldReference.FromLambda<object>(_ => System.Runtime.InteropServices.Marshal.SystemDefaultCharSize);
            var field4 = FieldReference.FromLambda<object>(_ => ImmutableList<int>.Empty);
            cx.AddTestExpr(Expression.StaticFieldAccess(field1).Dereference());
            cx.AddTestExpr(Expression.StaticFieldAccess(field2).Dereference());
            cx.AddTestExpr(Expression.StaticFieldAccess(field3).Dereference());
            cx.AddTestExpr(Expression.StaticFieldAccess(field4).Dereference());

            check.CheckOutput(cx);
        }

        [Fact]
        public void EqOperator()
        {
            cx.AddTestExpr(Expression.Binary("==", Expression.Constant<string>("abcd"), pString1), pString1);
            cx.AddTestExpr(Expression.Binary("==", Expression.Constant<string>(null), pString1), pString1);
            cx.AddTestExpr(Expression.Binary("!=", Expression.Constant<object>(null), Expression.ReferenceConversion(pString1, TypeSignature.Object)), pString1);
            cx.AddTestExpr(Expression.Binary("==", Expression.Constant(true), pBool1), pBool1);
            cx.AddTestExpr(Expression.Binary("==", Expression.Constant(true), Expression.Constant(false)));
            cx.AddTestExpr(Expression.Binary("==", Expression.Constant<int>(1), Expression.Constant<int>(1)));
            cx.AddTestExpr(Expression.Binary("==", Expression.Constant<UInt16>(1), Expression.Constant<UInt16>(1)));
            cx.AddTestExpr(Expression.Binary("==", Expression.Constant<ulong>(1), Expression.Constant<ulong>(1)));
            cx.AddTestExpr(Expression.Binary("==", Expression.Constant<float>(1), Expression.Constant<float>(1)));
            cx.AddTestExpr(Expression.Binary("==", Expression.Constant<double>(1), Expression.Constant<double>(1)));
            cx.AddTestExpr(Expression.Binary("!=", Expression.Constant<double>(1), Expression.Constant<double>(1)));

            check.CheckOutput(cx);
        }
    }
}
