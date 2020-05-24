using System;
using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    /// <summary> Expression that creates a new uninitialized array. </summary>
    /// <seealso cref="ExpressionFactory.MakeArray(Expression[])" />
    partial class NewArrayExpression
    {
        private static TypeReference[] AllowedDimensionTypes = new TypeReference[] {
            TypeSignature.Int32,
            TypeSignature.Int64
        };
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, NewArrayExpression obj)
        {
            if (obj.Type != null && obj.Type.Dimensions != obj.Dimensions.Length)
                e.Add(ValidationErrors.Create($"Expected {obj.Type.Dimensions} dimension parameters for construction of {obj.Type}, got {obj.Dimensions.Length}").Nest("dimensions"));

            for (int i = 0; i < obj.Dimensions.Length; i++)
            {
                var d = obj.Dimensions[i];
                if (Array.IndexOf(AllowedDimensionTypes, d.Type()) < 0)
                    e.Add(ValidationErrors.Create($"Array dimensions are expected to be of type int or long, not {d.Type()}").Nest(i.ToString()).Nest("dimensions")); // TODO: expression type validation
            }
        }
    }
}
