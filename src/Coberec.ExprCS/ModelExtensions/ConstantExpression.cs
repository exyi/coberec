using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    /// <summary> Expression that evaluates to the constant `Value`. Only supports primitive types and nulls. See <see cref="DefaultExpression" /> in case you want to create a default of a struct. </summary>
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
