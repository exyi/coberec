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
    }
}
