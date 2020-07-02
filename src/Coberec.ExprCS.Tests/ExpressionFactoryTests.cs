using System;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests
{
    public class ExpressionFactoryTests
    {
        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create();
        [Fact]
        public void ArrayInitializer()
        {
            cx.AddTestExpr(ExpressionFactory.MakeArray(TypeSignature.Boolean));
            var boolArray = ExpressionFactory.MakeArray(TypeSignature.Boolean, Expression.Constant(true), Expression.Constant(false));
            cx.AddTestExpr(boolArray);

            check.CheckOutput(cx);
            check.CheckString(boolArray.ToString());
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

        [Fact]
        public void String_Concat()
        {
            var pString = ParameterExpression.Create(TypeSignature.String, "s");
            cx.AddTestExpr(ExpressionFactory.String_Concat());
            cx.AddTestExpr(ExpressionFactory.String_Concat(pString), pString);
            cx.AddTestExpr(ExpressionFactory.String_Concat(pString, pString), pString);
            cx.AddTestExpr(ExpressionFactory.String_Concat(pString, Expression.Constant("; "), pString), pString);
            cx.AddTestExpr(ExpressionFactory.String_Concat(pString, Expression.Constant("; "), pString, Expression.Constant("; "), pString), pString);
            cx.AddTestExpr(ExpressionFactory.String_Concat(pString, Expression.Constant("; "), ExpressionFactory.String_Concat(pString, Expression.Constant("; "), pString, Expression.Constant("; "), pString), Expression.Constant("; "), pString), pString);

            check.CheckOutput(cx);
        }

        [Fact]
        public void String_Concat_Objects()
        {
            var pString = ParameterExpression.Create(TypeSignature.String, "s");
            var pInt = ParameterExpression.Create(TypeSignature.Int64, "int");
            var pIntNull = ParameterExpression.Create(TypeSignature.NullableOfT.Specialize(TypeSignature.Int64), "intn");
            cx.AddTestExpr(ExpressionFactory.String_Concat());
            cx.AddTestExpr(ExpressionFactory.String_Concat(pInt), pInt);
            cx.AddTestExpr(ExpressionFactory.String_Concat(Expression.Constant(12), Expression.Constant("--")));
            cx.AddTestExpr(ExpressionFactory.String_Concat(pInt, Expression.Constant("; "), pIntNull), pInt, pIntNull);

            check.CheckOutput(cx);
        }
    }
}
