using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    partial class ArrayIndexExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, ArrayIndexExpression obj)
        {
            if (obj.Array == null) return;

            var t = (obj.Array.Type() as TypeReference.ArrayTypeCase).Item;
            if (t is null)
                e.Add(ValidationErrors.Create($"Expected expression of array type, got '{obj.Array.Type()}'").Nest("array"));
            else if (t.Dimensions != obj.Indices.Length)
                e.Add(ValidationErrors.Create($"Expected {t.Dimensions} index parameters for indexing {t}, got {obj.Indices.Length}").Nest("indices"));
        }
    }
}
