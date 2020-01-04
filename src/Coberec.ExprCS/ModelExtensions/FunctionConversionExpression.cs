using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    partial class FunctionConversionExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, FunctionConversionExpression obj)
        {
            if (obj.Type is null || obj.Value is null) return;

            var from = obj.Value.Type().UnwrapReference();
            var into = obj.Type;

            if (!IsFunctionType(from))
                e.Add(ValidationErrors.Create($"Function conversion can only convert functions and delegates, but value has type '{from}'").Nest("value"));
            if (!IsFunctionType(into))
                e.Add(ValidationErrors.Create($"Function conversion can only convert functions and delegates, but target has type '{into}'").Nest("target"));

            if (from is TypeReference.FunctionTypeCase from_f && into is TypeReference.FunctionTypeCase into_f)
            {
                if (from_f.Item.Params.Length != into_f.Item.Params.Length)
                    e.Add(ValidationErrors.Create($"Can not convert from '{from}' to '{into}' as the parameter count is different").Nest("params").Nest("FunctionType").Nest("target"));
            }


            // TODO: add heuristics for System.Action and System.Func
        }

        static bool IsFunctionType(TypeReference type) =>
            type is TypeReference.FunctionTypeCase ||
            type is TypeReference.SpecializedTypeCase st && st.Item.Type.Kind == "delegate";
    }
}
