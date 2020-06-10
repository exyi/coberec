using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    public partial class BreakExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, BreakExpression b)
        {
            if (e.HasErrors) return;

            if (b.Value.Type() != b.Target.Type)
                e.Add(ValidationErrors.Create(
                    $"BreakExpression has value of type {b.Value.Type()}, but must return the same type type as target {b.Target}"
                ).Nest("value")); // TODO: validation path for expression type
        }
    }
}
