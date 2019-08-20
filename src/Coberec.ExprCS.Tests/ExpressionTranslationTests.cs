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
            var method = new MethodSignature(type, parameters.Select(p => new MethodArgument(p.Type, p.Name)).ToImmutableArray(), "M", expr.Type(), true, Accessibility.APublic, false, false, false, false, ImmutableArray<GenericParameter>.Empty);
            var methodDef = new MethodDef(method, parameters.ToImmutableArray(), expr);
            var typeDef = TypeDef.Empty(type).With(members: ImmutableArray.Create<MemberDef>(methodDef));

            cx.AddType(typeDef);
        }
    }

    public class ExpressionTranslationTests
    {
        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext MkContext() => MetadataContext.Create("MyModule");

        ParameterExpression p1 = ParameterExpression.Create(TypeSignature.Int32, "p1");
        ParameterExpression p2 = ParameterExpression.Create(TypeSignature.Int32, "p2");
        ParameterExpression pBool1 = ParameterExpression.Create(TypeSignature.Boolean, "pBool1");
        ParameterExpression pString1 = ParameterExpression.Create(TypeSignature.String, "pString1");

        public ExpressionTranslationTests()
        {
            RaiseExceptionDebugProvider.HackIt();
        }


        [Fact]
        public void ArgumentPassing()
        {
            var cx = MkContext();
            cx.AddTestExpr(p1, p1);
            check.CheckOutput(cx);
        }

        [Fact]
        public void VariableDeclaration()
        {
            var cx = MkContext();
            cx.AddTestExpr(Expression.LetIn(p2, p1, p2), p1);
            check.CheckOutput(cx);
        }

        [Fact]
        public void MethodInvocation()
        {
            var cx = MkContext();

            var intParse = cx.GetMemberMethods(TypeSignature.Int32).Single(m => m.Name == "Parse" && m.Args.Length == 1 && m.Args[0].Type == TypeSignature.String);
            var stringConcat = cx.GetMemberMethods(TypeSignature.String).Single(m => m.Name == "Concat" && m.Args.Length == 2 && m.Args[0].Type == TypeSignature.String);

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
            var cx = MkContext();
            var e = Expression.Conditional(pBool1, p1, p2);
            cx.AddTestExpr(e, pBool1, p1, p2);
            check.CheckOutput(cx);
        }

        [Fact]
        public void InfiniteLoop()
        {
            var cx = MkContext();
            var call = ExampleMethodCall(cx);

            var loop = Expression.Loop(call);

            cx.AddTestExpr(loop);
            check.CheckOutput(cx);
        }

        static Expression ExampleMethodCall(MetadataContext cx)
        {
            var intParse = cx.GetMemberMethods(TypeSignature.Int32).Single(m => m.Name == "Parse" && m.Args.Length == 1 && m.Args[0].Type == TypeSignature.String);
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
            var cx = MkContext();
            LabelTarget label = LabelTarget.New("l");
            Expression @break = MakeExampleBreak(cx, label);

            var b = Expression.Breakable(@break, label);
            cx.AddTestExpr(b);
            check.CheckOutput(cx);
        }

        [Fact]
        public void BreakableInsideBlock()
        {
            var cx = MkContext();
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
            var cx = MkContext();
            var e = Expression.While(ExampleCondition(cx), ExampleMethodCall(cx));
            cx.AddTestExpr(e);
            check.CheckOutput(cx);
        }
    }
}
