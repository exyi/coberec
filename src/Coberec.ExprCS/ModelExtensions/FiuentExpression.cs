namespace Coberec.ExprCS
{
    public static class FiuentExpression
    {
        public static Expression Dereference(this Expression expr) => Expression.Dereference(expr);
        public static Expression ReferenceAssign(this Expression target, Expression value) => Expression.ReferenceAssign(target, value);
    }
}
