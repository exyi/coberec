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
            var name = cx.DefinedTypes.Count < 3 ? ((char)('C' + cx.DefinedTypes.Count)).ToString() : "C" + cx.DefinedTypes.Count;
            var ns = ((TypeOrNamespace.NamespaceSignatureCase)cx.DefinedTypes.FirstOrDefault()?.Signature.Parent)?.Item ??
                    new NamespaceSignature("NS", null);
            var type = new TypeSignature(name, ns, false, false, Accessibility.APublic, 0);
            var method = new MethodSignature(type, parameters.Select(p => new MethodParameter(p.Type, p.Name)).ToImmutableArray(), "M", expr.Type(), true, Accessibility.APublic, false, false, false, false, ImmutableArray<GenericParameter>.Empty);
            var methodDef = new MethodDef(method, parameters.ToImmutableArray(), expr);
            var typeDef = TypeDef.Empty(type).With(members: ImmutableArray.Create<MemberDef>(methodDef));

            cx.AddType(typeDef);
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
            var intParse = cx.GetMemberMethods(TypeSignature.Int32).Single(m => m.Name == "Parse" && m.Params.Length == 1 && m.Params[0].Type == TypeSignature.String);
            var stringConcat = cx.GetMemberMethods(TypeSignature.String).Single(m => m.Name == "Concat" && m.Params.Length == 2 && m.Params[0].Type == TypeSignature.String);

            var concatCall = Expression.MethodCall(stringConcat, ImmutableArray.Create(Expression.Constant("123456789", TypeSignature.String), pString1), null);
            var intMethod = Expression.MethodCall(intParse, ImmutableArray.Create(concatCall), null);
            var voidMethod = Expression.Block(ImmutableArray.Create(intMethod), Expression.Default(TypeSignature.Void));

            cx.AddTestExpr(intMethod, pString1);
            cx.AddTestExpr(Expression.LetIn(pString1, Expression.Constant("5", TypeSignature.String), intMethod));
            cx.AddTestExpr(voidMethod, pString1);
            cx.AddTestExpr(Expression.Block(ImmutableArray.Create(concatCall), Expression.Default(TypeSignature.Void)), pString1);

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
            var intParse = cx.GetMemberMethods(TypeSignature.Int32).Single(m => m.Name == "Parse" && m.Params.Length == 1 && m.Params[0].Type == TypeSignature.String);
            return Expression.MethodCall(intParse, ImmutableArray.Create(Expression.Constant("123456789", TypeSignature.String)), null);
        }

        static Expression MakeExampleBreak(MetadataContext cx, LabelTarget label)
        {
            var call = ExampleMethodCall(cx);
            var condition = ExampleCondition(cx);

            var ifBlock = Expression.IfThen(
                condition,
                Expression.Break(Expression.Default(TypeSignature.Void), label));

            return Expression.Block(ImmutableArray.Create<Expression>(
                call,
                ifBlock,
                call
            ), Expression.Default(TypeSignature.Void));
        }

        static Expression ExampleCondition(MetadataContext cx)
        {
            var thread = cx.FindTypeDef(typeof(System.Threading.Thread));
            var currentThread = cx.GetMemberProperties(thread).Single(m => m.Name == "CurrentThread").Getter;
            var isBackground = cx.GetMemberProperties(((TypeReference.SpecializedTypeCase)currentThread.ResultType).Item.Type).Single(m => m.Name == "IsBackground").Getter;
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
            ), Expression.Constant(12, TypeSignature.Int32));
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

        MethodSignature toStringMethod => cx.GetMemberMethods(TypeSignature.Object).Single(m => m.Name == "ToString");

        [Fact]
        public void ValueBoxing()
        {
            var e = Expression.ReferenceConversion(Expression.Constant(1, TypeSignature.Int32), TypeSignature.Object);
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
            var fn1 = Expression.Function(Expression.Constant(1, TypeSignature.Int32));
            var v1 = ParameterExpression.Create(fn1.Type(), "v1");
            var fn2 = Expression.Function(
                        Expression.Conditional(
                            pBool1,
                            Expression.ReferenceConversion(Expression.Invoke(v1, ImmutableArray<Expression>.Empty), TypeSignature.Object),
                            Expression.Constant(null, TypeSignature.Object)
                        ),
                        pBool1);
            var v2 = ParameterExpression.Create(fn2.Type(), "v2");

            cx.AddTestExpr(
                Expression.LetIn(v1, fn1, Expression.LetIn(v2, fn2,
                    Expression.Invoke(v2, ImmutableArray.Create(Expression.Constant(true, TypeSignature.Boolean)))
                )));
            check.CheckOutput(cx);
        }

        [Fact]
        public void LambdaFunction()
        {
            var fn1 = Expression.Function(Expression.Constant(1, TypeSignature.Int32));
            var fn2 = Expression.Function(
                        Expression.Conditional(
                            pBool1,
                            Expression.ReferenceConversion(Expression.Invoke(fn1, ImmutableArray<Expression>.Empty), TypeSignature.Object),
                            Expression.Constant(null, TypeSignature.Object)
                        ),
                        pBool1);

            cx.AddTestExpr(
                Expression.Invoke(fn2, ImmutableArray.Create(Expression.Constant(true, TypeSignature.Boolean)))
            );
            check.CheckOutput(cx);
        }
    }
}
