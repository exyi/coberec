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
    }
}
