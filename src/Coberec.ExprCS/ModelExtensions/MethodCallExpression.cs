using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    partial class MethodCallExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, MethodCallExpression me)
        {
            if (me.Method is null) return;

            var m = me.Method;
            if (m.Signature.IsStatic && me.Target is object)
                e.Add(ValidationErrors.Create($"Static method must not have target set.").Nest("target"));
            if (!m.Signature.IsStatic && me.Target is null)
                e.Add(ValidationErrors.Create($"Instance method must have target set.").Nest("target"));

            if (!m.Signature.IsStatic && me.Target is object)
                if (m.DeclaringType() != me.Target.Type().UnwrapReference())
                    e.Add(ValidationErrors.Create($"Instance method declared on '{m.DeclaringType()}' can not be invoked with target of type '{me.Target.Type().UnwrapReference()}'").Nest("target"));
            if (me.Args.Length != m.Signature.Params.Length)
                e.Add(ValidationErrors.Create($"Can not call method '{m}' with {me.Args.Length} arguments."));
            else
            {
                var p = m.Params();
                for (int i = 0; i < me.Args.Length; i++)
                    if (me.Args[i] is object && p[i].Type != me.Args[i].Type())
                        e.Add(ValidationErrors.Create($"Method '{m}' does not accept value of type {me.Args[i].Type()}.").Nest(i.ToString()).Nest("args"));
            }

            if (m.Signature.HasSpecialName)
            {
                e.Add(m.Signature.Name switch {
                    ".cctor" => ValidationErrors.Create("Can not call static constructor from expression").Nest("signature").Nest("method"),
                    ".ctor" =>
                        (me.Target is Expression.ParameterCase) || (me.Target is Expression.ReferenceConversionCase rce && rce.Item.Value is Expression.ParameterCase) ? null :
                        ValidationErrors.Create("Constructor can only be invoked directly on this parameter").Nest("signature").Nest("method"),
                    _ => null
                });
            }
        }
    }
}
