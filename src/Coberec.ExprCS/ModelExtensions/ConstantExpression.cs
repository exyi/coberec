using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    partial class ConstantExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, ConstantExpression obj)
        {
            if (obj.Type is null) return;

            if (obj.Value is null)
            {
                if (obj.Type.IsReferenceType == false && !obj.Type.IsNullableValueType())
                    e.Add(ValidationErrors.Create($"Can not have constant null for non-nullable type '{obj.Type}'. Use Expression.Default for default value."));
                return;
            }
            // TODO: somehow check primitiveness
        }
    }
}
