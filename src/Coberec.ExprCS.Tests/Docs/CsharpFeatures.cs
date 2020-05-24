using System;
using System.Collections.Generic;
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
            FieldReference field1 = FieldReference.FromLambda<(string, int)>(s => s.Item1);
            FieldReference field2 = FieldReference.FromLambda<(string, int)>(s => s.Item2);

            Expression value = pTuple.Read().AccessField(field1).Dereference();
            Expression value2 = pTuple.Ref().AccessField(field1).Dereference();
            Expression assignment = pTuple.Ref().AccessField(field1).ReferenceAssign(Expression.Default(field1.ResultType()));

            FieldReference staticField = FieldReference.FromLambda<object>(_ => ImmutableArray<int>.Empty);
            var value3 = Expression.StaticFieldAccess(staticField).Dereference();

            cx.AddTestExpr(value, pTuple);
            cx.AddTestExpr(value2, pTuple);
            cx.AddTestExpr(assignment, pTuple);
            cx.AddTestExpr(value3);

            cx.AddTestExpr(pTuple.Ref().FieldCompoundAssign(field2, "+", Expression.Constant(1)), pTuple);

            check.CheckOutput(cx);
        }

        [Fact]
        public void AccessingProperties()
        {
            PropertyReference prop = PropertyReference.FromLambda<UriBuilder>(s => s.Port);
            Expression target = pUriBuilder;

            Expression value = target.ReadProperty(prop);
            Expression assignment = target.AssignProperty(prop, Expression.Constant(8080));

            cx.AddTestExpr(value, pUriBuilder);
            cx.AddTestExpr(assignment, pUriBuilder);

            cx.AddTestExpr(target.PropertyCompoundAssign(prop, "+", Expression.Constant(10)), pUriBuilder);

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

        [Fact]
        public void NullCoalesce()
        {
            MethodReference readLineM = MethodReference.FromLambda(() => Console.ReadLine());
            cx.AddTestExpr(pString1.Read().NullCoalesce(Expression.Constant("<null>")), pString1);
            cx.AddTestExpr(Expression.StaticMethodCall(readLineM).NullCoalesce(Expression.Constant("<null>")));
            cx.AddTestExpr(pNullInt.Read().NullCoalesce(ExpressionFactory.Nullable_Create(Expression.Constant(-1))), pNullInt);
            // cx.AddTestExpr(pNullInt.Read().NullCoalesce(Expression.Constant(-1)), pNullInt);

            check.CheckOutput(cx);
        }

        [Fact]
        public void NewObject()
        {
            var ctor = MethodReference.FromLambda(() => new System.Collections.Generic.List<int>(0));
            cx.AddTestExpr(Expression.NewObject(ctor, Expression.Constant(100)));

            check.CheckOutput(cx);
        }

        [Fact]
        public void Boxing()
        {
            cx.AddTestExpr(pTime.Read().Box(), pTime);
            cx.AddTestExpr(pTime.Read().ReferenceConvert(TypeReference.FromType(typeof(IEquatable<TimeSpan>))), pTime);
            // the same thing works for reference types, although it's not really boxing
            cx.AddTestExpr(pString1.Read().Box(), pString1);
            cx.AddTestExpr(pString1.Read().ReferenceConvert(TypeReference.FromType(typeof(IEnumerable<char>))), pString1);

            check.CheckOutput(cx);
        }

        [Fact]
        public void Functions()
        {
            // parameterless `() => 1`

            Expression fn1 = Expression.Function(Expression.Constant(1));

            // (bool a) => a ? 1 : 2

            ParameterExpression pA = ParameterExpression.Create(TypeSignature.Boolean, "a");
            Expression fn2 = Expression.Function(
                Expression.Conditional(pA, Expression.Constant(1), Expression.Constant(2)),
                pA
            );

            cx.AddTestExpr(fn1.Invoke());
            cx.AddTestExpr(fn2.Invoke(Expression.Constant(true)));

            var func = TypeReference.FromType(typeof(Func<bool, int>));
            cx.AddTestExpr(fn2.FunctionConvert(func));

            check.CheckOutput(cx);
        }

        [Fact]
        public void LocalFunction()
        {
            ParameterExpression pA = ParameterExpression.Create(TypeSignature.Boolean, "a");
            Expression fn2 = Expression.Function(
                Expression.Conditional(pA, Expression.Constant(1), Expression.Constant(2)),
                pA
            );
            ParameterExpression localFn2 = ParameterExpression.Create(fn2.Type(), "fn2");

            cx.AddTestExpr(
                Expression.Binary("+",
                    localFn2.Read().Invoke(Expression.Constant(true)),
                    localFn2.Read().Invoke(Expression.Constant(false))
                )
                .Where(localFn2, fn2)
            );

            check.CheckOutput(cx);
        }

        [Fact]
        public void Variables()
        {
            var myVar = ParameterExpression.Create(TypeSignature.Int32, "myVar");

            cx.AddTestExpr(Expression.LetIn(
                myVar, Expression.Constant(42),
                target: Expression.Binary("+", myVar, myVar)
            ));

            var mutableVar = ParameterExpression.CreateMutable(TypeSignature.Int32, "mutableVar");

            cx.AddTestExpr(Expression.LetIn(
                mutableVar, Expression.Constant(42),
                target: Expression.Binary("+",
                    mutableVar,
                    new [] {
                        mutableVar.Assign(Expression.Constant(30))
                    }.ToBlock(mutableVar)
                )
            ));

            var declaringType = TypeSignature.StaticClass("TestClass", NamespaceSignature.Parse("NS"), Accessibility.APublic);
            var method = MethodSignature.Static(
                "M",
                declaringType,
                Accessibility.APublic,
                TypeSignature.Int32,
                new MethodParameter(TypeSignature.Int32, "a"));
            var argA = ParameterExpression.CreateMutable(TypeSignature.Int32, "a");
            var body = new [] {
                argA.Assign(Expression.Binary("+", argA, Expression.Constant(1)))
            }.ToBlock(result: argA);
            var methodDef = new MethodDef(method, new [] { argA }, body);

            cx.AddType(TypeDef.Empty(declaringType).AddMember(methodDef));

            check.CheckOutput(cx);
        }


        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create();
        ParameterExpression p1 = ParameterExpression.Create(TypeSignature.Int32, "p1");
        ParameterExpression p2 = ParameterExpression.Create(TypeSignature.Int32, "p2");
        ParameterExpression pBool1 = ParameterExpression.Create(TypeSignature.Boolean, "pBool1");
        ParameterExpression pString1 = ParameterExpression.Create(TypeSignature.String, "pString1");
        ParameterExpression pNullInt = ParameterExpression.Create(TypeSignature.NullableOfT.Specialize(TypeSignature.Int32), "pNullInt");
        ParameterExpression pObject = ParameterExpression.Create(TypeSignature.Object, "pObject");
        ParameterExpression pTime = ParameterExpression.Create(TypeSignature.TimeSpan, "pTime");
        ParameterExpression pTuple = ParameterExpression.Create(TypeReference.FromType(typeof(ValueTuple<string, int>)), "pTuple");
        ParameterExpression pUriBuilder = ParameterExpression.Create(TypeReference.FromType(typeof(UriBuilder)), "pUriBuilder");
    }
}
