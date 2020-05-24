using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    /// <summary> Expression represents access to `Field` on object `Target`. If `Field` is static, `Target` is null. </summary>
    partial class FieldAccessExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, FieldAccessExpression obj)
        {
            if (obj.Field is null) return;

            var f = obj.Field;
            if (f.Signature.IsStatic && obj.Target is object)
                e.Add(ValidationErrors.Create($"Static field must not have target set.").Nest("target"));
            if (!f.Signature.IsStatic && obj.Target is null)
                e.Add(ValidationErrors.Create($"Instance fields must have target set.").Nest("target"));

            if (!f.Signature.IsStatic && obj.Target is object)
                if (f.DeclaringType() != obj.Target.Type().UnwrapReference())
                    e.Add(ValidationErrors.Create($"Instance field declared on '{f.DeclaringType()}' can not be invoked with target of type '{obj.Target.Type().UnwrapReference()}'").Nest("target"));
        }
    }
}
