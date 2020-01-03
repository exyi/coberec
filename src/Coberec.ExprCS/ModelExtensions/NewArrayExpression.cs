using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    partial class NewArrayExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, NewArrayExpression obj)
        {
            if (obj.Type != null && obj.Type.Dimensions != obj.Dimensions.Length)
                e.Add(ValidationErrors.Create($"Expected {obj.Type.Dimensions} dimension parameters for construction of {obj.Type}, got {obj.Dimensions.Length}").Nest("dimensions"));
        }
    }
}
