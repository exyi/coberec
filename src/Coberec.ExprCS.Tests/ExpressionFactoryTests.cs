using System;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests
{
    public class ExpressionFactoryTests
    {
        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create("MyModule");
        [Fact]
        public void ArrayInitializer()
        {
            cx.AddTestExpr(ExpressionFactory.MakeArray(TypeSignature.Boolean));
            cx.AddTestExpr(ExpressionFactory.MakeArray(TypeSignature.Boolean, Expression.Constant(true), Expression.Constant(false)));

            check.CheckOutput(cx);
        }

        [Fact]
        public void ArrayInitializer_TypeCheck()
        {
            check.CheckException(
                () => ExpressionFactory.MakeArray(Expression.Constant(true), Expression.Constant(1))
            );
        }

        [Fact]
        public void Nullable_Read()
        {
            var p = ParameterExpression.Create(TypeReference.FromType(typeof(int?)), "a");

            cx.AddTestExpr(ExpressionFactory.Nullable_HasValue(p), p);
            cx.AddTestExpr(ExpressionFactory.Nullable_Value(p), p);

            check.CheckOutput(cx);
        }

        [Fact]
        public void Nullable_Create()
        {

            cx.AddTestExpr(ExpressionFactory.Nullable_Create(Expression.Constant(123)));
            cx.AddTestExpr(Expression.Default(TypeReference.FromType(typeof(int?))));

            check.CheckOutput(cx);
        }
    }
}
