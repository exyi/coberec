using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    /// <summary> Expression that represents an assignment into a reference. Used to assign into variables and fields. </summary>
    /// <seealso cref="ByReferenceType" />
    partial class ReferenceAssignExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, ReferenceAssignExpression obj)
        {
            if (obj.Target is null || obj.Value is null) return;

            var type = obj.Target.Type().UnwrapReference(); // the reference is checked by previous validations

            if (type != obj.Value.Type())
                e.Add(ValidationErrors.Create($"Can not assign '{obj.Value.Type()}' into reference '{obj.Target.Type()}'. The types must match"));
        }
    }
}
