using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Coberec.ExprCS
{
    public static class FluentExpression
    {
        public static Expression Dereference(this Expression expr) => Expression.Dereference(expr);
        public static Expression ReferenceAssign(this Expression target, Expression value) => Expression.ReferenceAssign(target, value);

        /// <summary> <see cref="LetInExpression" />, just with the variable declaration after it is used (syntactically only, of course). Inspired by Haskell's `where` keyword. </summary>
        public static Expression Where(this Expression target, ParameterExpression variable, Expression value) =>
            Expression.LetIn(variable, value, target);

        /// <summary> Collects all expression in the collection and puts them into a block expression. The <paramref name="result" /> specifies the result value returned after all the previous expressions are evaluated. </summary>
        public static Expression ToBlock(this IEnumerable<Expression> expressions, Expression result = null) =>
            Expression.Block(expressions.ToImmutableArray(), result ?? Expression.Nop);


        /// <summary> Calls the specified instance method on the <paramref name="target" />. Can be also used to call extension methods </summary>
        public static Expression CallMethod(this Expression target, MethodReference method, params Expression[] args)
        {
            if (method.Signature.IsStatic)
                // probably extension method
                return Expression.StaticMethodCall(method, args.Prepend(target));
            else
            {
                Assert.Equal(method.Params().Select(p => p.Type), args.Select(a => a.Type()));
                return Expression.MethodCall(method, args.ToImmutableArray(), target);
            }
        }

        /// <summary> Gets a reference pointing to the instance <paramref name="field" /> on the <paramref name="target" /> </summary>
        public static Expression AccessField(this Expression target, FieldReference field) =>
            Expression.FieldAccess(field, target);

        /// <summary> Creates a reference conversion of <paramref name="value" /> to <paramref name="type" /> </summary>
        public static Expression ReferenceConvert(this Expression value, TypeReference type) =>
            Expression.ReferenceConversion(value, type);

        /// <summary> Creates a reference conversion of <paramref name="value" /> to <see cref="System.Object" /> </summary>
        public static Expression Box(this Expression value) =>
            Expression.ReferenceConversion(value, TypeSignature.Object);
    }
}
