using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    /// <summary> Expression representing a by-reference object cast. Also does boxing and unboxing. </summary>
    partial class ReferenceConversionExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, ReferenceConversionExpression obj)
        {
            if (obj.Type is null || obj.Value is null) return;

            var from = obj.Value.Type().UnwrapReference();
            var into = obj.Type;
            if (from == into)
                return; // this is valid

            if (from.IsSealed() && into.IsSealed())
                e.Add(ValidationErrors.Create($"Can not convert from '{from}' to '{into}' since both types are sealed."));

            // yes, there is room for other checks
        }
    }
}
