using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    /// <summary> Expression that creates a new object and invokes a constructor. Equivalent of `new Ctor(Args)`. </summary>
    partial class NewObjectExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, NewObjectExpression ne)
        {
            if (ne.Ctor is null) return;

            var m = ne.Ctor;
            if (!m.Signature.IsConstructor())
                e.Add(ValidationErrors.Create($"{m} must be a constructor").Nest("ctor"));

            if (m.Signature.DeclaringType.IsAbstract)
                e.Add(ValidationErrors.Create($"Can not create instance of abstract type '{m.DeclaringType()}'").Nest("isAbstract").Nest("signature").Nest("ctor"));

            if (ne.Args.Length != m.Signature.Params.Length)
                e.Add(ValidationErrors.Create($"Can not call constructor '{m}' with {ne.Args.Length} arguments."));
            else
            {
                var p = m.Params();
                for (int i = 0; i < ne.Args.Length; i++)
                    if (ne.Args[i] is object && p[i].Type != ne.Args[i].Type())
                        e.Add(ValidationErrors.Create($"Constructor '{m}' does not accept value of type {ne.Args[i].Type()}.").Nest(i.ToString()).Nest("args"));
            }
        }
    }
}
