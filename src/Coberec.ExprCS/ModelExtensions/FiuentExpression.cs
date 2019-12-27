using System.Collections.Generic;
using System.Collections.Immutable;

namespace Coberec.ExprCS
{
    public static class FiuentExpression
    {
        public static Expression Dereference(this Expression expr) => Expression.Dereference(expr);
        public static Expression ReferenceAssign(this Expression target, Expression value) => Expression.ReferenceAssign(target, value);

        /// <summary> <see cref="LetInExpression" />, just with the variable declaration after it is used (syntactically only, of course). Inspired by Haskell's `where` keyword. </summary>
        public static Expression Where(this Expression target, ParameterExpression variable, Expression value) =>
            Expression.LetIn(variable, value, target);

        /// <summary> Collects all expression in the collection and puts them into a block expression. The <paramref name="result" /> specifies the result value returned after all the previous expressions are evaluated. </summary>
        public static Expression ToBlock(this IEnumerable<Expression> expressions, Expression result = null) =>
            Expression.Block(expressions.ToImmutableArray(), result ?? Expression.Nop);
    }
}
