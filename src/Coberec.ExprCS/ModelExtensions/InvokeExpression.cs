using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    partial class InvokeExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, InvokeExpression ie)
        {
            if (ie.Function is null) return;

            var ftype = (ie.Function.Type() as TypeReference.FunctionTypeCase)?.Item;
            if (ftype is null)
            {
                e.Add(ValidationErrors.Create($"Invoked function must be of function type, got '{ie.Function.Type()}'. Maybe, you can use FunctionConversion to convert delegate to a function type.").Nest("function"));
                return;
            }

            if (ie.Args.Length != ftype.Params.Length)
                e.Add(ValidationErrors.Create($"Can not invoke function '{ftype}' with {ie.Args.Length} arguments."));
            else
            {
                var p = ftype.Params;
                for (int i = 0; i < p.Length; i++)
                    if (ie.Args[i] is object && p[i].Type != ie.Args[i].Type())
                        e.Add(ValidationErrors.Create($"Function '{ftype}' does not accept value of type {ie.Args[i].Type()}.").Nest(i.ToString()).Nest("args"));
            }
        }
    }
}
