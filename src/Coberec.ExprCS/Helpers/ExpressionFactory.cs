using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;

namespace Coberec.ExprCS
{
    /// <summary> Helper functions for creating more complex expressions </summary>
    public static class ExpressionFactory
    {
        /// <summary> Creates new array from the specified items. It is roughly equivalent to C# array initializers. </summary>
        public static Expression MakeArray(IEnumerable<Expression> items)
        {
            var itemsA = items.ToImmutableArray();
            if (itemsA.Length == 0)
                throw new ArgumentException("Items must not be empty list. To create an empty array use Expression.NewArray or ExpressionFactory.MakeArray(TypeReference, items)", nameof(items));
            var type = itemsA[0].Type();
            return MakeArray(type, itemsA);
        }

        /// <summary> Creates new array from the specified items. It is roughly equivalent to C# array initializers. This overload can create empty arrays. </summary>
        public static Expression MakeArray(TypeReference type, IEnumerable<Expression> items)
        {
            var itemsA = items.ToImmutableArray();

            foreach (var i in itemsA)
                if (i.Type() != type)
                    throw new ArgumentException($"Items of type {type} were expected, but item {i} has type {i.Type()}", nameof(items));

            var arrayType = new ArrayType(type, dimensions: 1);
            var tmp = ParameterExpression.Create(arrayType, "tmpArray");
            return
                itemsA.EagerSelect((item, index) =>
                    Expression.ArrayIndex(tmp, Expression.Constant(index))
                    .ReferenceAssign(item)
                )
                .ToBlock(result: tmp)
                .Where(tmp, Expression.NewArray(arrayType, Expression.Constant(itemsA.Length)));
        }

        /// <summary> Creates new array from the specified items. It is roughly equivalent to C# array initializers. </summary>
        public static Expression MakeArray(params Expression[] items) => MakeArray(items.AsEnumerable());

        /// <summary> Creates new array from the specified items. It is roughly equivalent to C# array initializers. This overload can create empty arrays. </summary>
        public static Expression MakeArray(TypeReference type, params Expression[] items) => MakeArray(type, items.AsEnumerable());

        public static Expression Nullable_HasValue(Expression target)
        {
            if (target.Type().UnwrapNullableValueType() is TypeReference elementType)
            {
                return target.CallMethod(
                    PropertySignature.Nullable_HasValue.Specialize(elementType).Getter()
                );
            }
            else
                throw new ArgumentException($"Target `{target}` must be of type Nullable<...>, not {target.Type()}.", nameof(target));
        }

        public static Expression Nullable_Value(Expression target)
        {
            if (target.Type().UnwrapNullableValueType() is TypeReference elementType)
            {
                return target.CallMethod(
                    PropertySignature.Nullable_Value.Specialize(elementType).Getter()
                );
            }
            else
                throw new ArgumentException($"Target `{target}` must be of type Nullable<...>, not {target.Type()}.", nameof(target));
        }

        public static Expression Nullable_Create(Expression value)
        {
            return Expression.NewObject(
                MethodSignature.NullableOfT_Constructor.Specialize(new[] { value.Type() }, new TypeReference[] {}),
                ImmutableArray.Create(value)
            );
        }

        /// <summary> Concatenates the specified <paramref name="expressions" /> as strings. The expressions don't have to be of type string. </summary>
        public static Expression String_Concat(IEnumerable<Expression> expressions) => String_Concat(expressions.ToImmutableArray());
        /// <summary> Concatenates the specified <paramref name="expressions" /> as strings. The expressions don't have to be of type string. </summary>
        public static Expression String_Concat(params Expression[] expressions) => String_Concat(expressions.ToImmutableArray());
        /// <summary> Concatenates the specified <paramref name="expressions" /> as strings. The expressions don't have to be of type string. </summary>
        public static Expression String_Concat(ImmutableArray<Expression> expressions)
        {
            // Check if there are follow-up constants
            for (int i = 1; i < expressions.Length; i++)
            {
                if (expressions[i - 1] is Expression.ConstantCase && expressions[i] is Expression.ConstantCase)
                {
                    var newExpr = ImmutableArray.CreateBuilder<Expression>();
                    var c = "";
                    foreach (var e in expressions)
                    {
                        if (e is Expression.ConstantCase expr)
                            c += expr.Item.Value;
                        else
                        {
                            if (c.Length > 0) newExpr.Add(Expression.Constant(c));
                            c = "";
                            newExpr.Add(e);
                        }
                    }
                    if (c.Length > 0) newExpr.Add(Expression.Constant(c));
                    expressions = newExpr.ToImmutable();
                    break;
                }
            }

            var allString = expressions.All(e => e.Type() == TypeSignature.String);

            if (expressions.Length == 0) return Expression.Constant("");
            if (expressions.Length == 1 && expressions[0] is Expression.ConstantCase ce)
                // string concat replaces nulls with empty strings, which is a big difference from object.ToString
                return Expression.Constant("" + ce.Item.Value);
            if (expressions.Length == 1)
                expressions = expressions.Add(Expression.Constant(""));
            if (allString)
                return String_Concat_Strings(expressions);
            else
                return String_Concat_Objects(
                    expressions.EagerSelect(FluentExpression.Box)
                );
        }

        private static Expression String_Concat_Objects(ImmutableArray<Expression> expressions)
        {
            if (expressions.Length == 2)
                return Expression.StaticMethodCall(
                    MethodReference.FromLambda(() => String.Concat(new object(), null)),
                    expressions
                );
            if (expressions.Length == 3)
                return Expression.StaticMethodCall(
                    MethodReference.FromLambda(() => String.Concat(new object(), null, null)),
                    expressions
                );
            return Expression.StaticMethodCall(
                MethodReference.FromLambda(() => String.Concat(new object[100])),
                MakeArray(TypeSignature.Object, expressions)
            );
        }

        private static Expression String_Concat_Strings(ImmutableArray<Expression> expressions)
        {
            if (expressions.Length == 2)
                return Expression.StaticMethodCall(
                    MethodReference.FromLambda(() => String.Concat("", "")),
                    expressions
                );
            if (expressions.Length == 3)
                return Expression.StaticMethodCall(
                    MethodReference.FromLambda(() => String.Concat("", "", "")),
                    expressions
                );
            if (expressions.Length == 4)
                return Expression.StaticMethodCall(
                    MethodReference.FromLambda(() => String.Concat("", "", "", "")),
                    expressions
                );
            return Expression.StaticMethodCall(
                MethodReference.FromLambda(() => String.Concat(new string[100])),
                MakeArray(TypeSignature.String, expressions)
            );
        }
    }
}
