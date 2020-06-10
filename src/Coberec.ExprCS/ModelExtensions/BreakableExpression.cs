using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    public partial class BreakableExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, BreakableExpression b)
        {
            if (e.HasErrors) return;

            if (b.Expression.Type() != b.Label.Type)
                e.Add(ValidationErrors.Create(
                    $"BreakableExpression returns {b.Expression.Type()}, but must return the same type type as {b.Label}"
                ).Nest("expression")); // TODO: validation path for expression type
        }
    }
}
