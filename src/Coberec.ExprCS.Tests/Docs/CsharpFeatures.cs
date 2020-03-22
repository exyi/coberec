using System;
using System.Collections.Immutable;
using System.Linq;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests.Docs
{
    public class CsharpFeatures
    {

        // these tests should check that the code provided in the docs actually works

        [Fact]
        public void AccessingFields()
        {
            FieldReference field = FieldReference.FromLambda<(string, int)>(s => s.Item1);
            Expression target = pTuple;

            Expression value = target.AccessField(field).Dereference();
            Expression assignment = target.AccessField(field).ReferenceAssign(Expression.Default(field.ResultType()));

            FieldReference staticField = FieldReference.FromLambda<object>(_ => ImmutableArray<int>.Empty);
            var value2 = Expression.StaticFieldAccess(staticField).Dereference();

            cx.AddTestExpr(value, pTuple);
            cx.AddTestExpr(assignment, pTuple);
            cx.AddTestExpr(value2);

            check.CheckOutput(cx);
        }

        [Fact]
        public void Conditions()
        {
            Expression myString = pString1.Read();
            Expression cond = Expression.Conditional(
                myString.IsNull(),
                Expression.Constant("<empty>"),
                myString
            );
            cx.AddTestExpr(cond, pString1);
            Expression a = p1.Read();
            Expression cond2 = Expression.Conditional(
                Expression.Binary(">", a, Expression.Constant(10)),
                Expression.StaticMethodCall(
                    MethodReference.FromLambda(() => Console.WriteLine(1)),
                    a),
                Expression.Nop
            );
            cx.AddTestExpr(cond2, p1);
            Expression cond3 = Expression.IfThen(
                Expression.Binary(">", a, Expression.Constant(10)),
                Expression.StaticMethodCall(
                    MethodReference.FromLambda(() => Console.WriteLine(1)),
                    a)
            );
            cx.AddTestExpr(cond3, p1);

            check.CheckOutput(cx);
        }

        [Fact]
        public void Blocks()
        {
            MethodReference writeLineM = MethodReference.FromLambda(() => Console.WriteLine(""));
            MethodReference readLineM = MethodReference.FromLambda(() => Console.ReadLine());
            Expression expr = Expression.Block(
                ImmutableArray.Create(
                    Expression.StaticMethodCall(
                        writeLineM,
                        Expression.Constant("Enter the output file path: ")
                    )
                ),
                Expression.StaticMethodCall(readLineM)
            );
            cx.AddTestExpr(expr);

            Expression listOfWrites =
                Enumerable.Range(1, 30)
                .Select(i => $"Line {i}")
                .Select(Expression.Constant)
                .Select(a => Expression.StaticMethodCall(writeLineM, a))
                .ToBlock();
            cx.AddTestExpr(listOfWrites);

            check.CheckOutput(cx);
        }



        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create("MyModule");
        ParameterExpression p1 = ParameterExpression.Create(TypeSignature.Int32, "p1");
        ParameterExpression p2 = ParameterExpression.Create(TypeSignature.Int32, "p2");
        ParameterExpression pBool1 = ParameterExpression.Create(TypeSignature.Boolean, "pBool1");
        ParameterExpression pString1 = ParameterExpression.Create(TypeSignature.String, "pString1");
        ParameterExpression pObject = ParameterExpression.Create(TypeSignature.Object, "pObject");
        ParameterExpression pTime = ParameterExpression.Create(TypeSignature.TimeSpan, "pTime");
        ParameterExpression pTuple = ParameterExpression.Create(TypeReference.FromType(typeof(ValueTuple<string, int>)), "pTuple");
    }
}
