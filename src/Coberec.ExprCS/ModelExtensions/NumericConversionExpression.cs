using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    /// <summary> Expression gets array element given by an index. Equivalent of `array[...indices]` </summary>
    partial class NumericConversionExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, NumericConversionExpression n)
        {
            if (e.HasErrors) return;

            bool isNumericSignature(TypeSignature ts) =>
                ts == TypeSignature.Byte ||
                ts == TypeSignature.SByte ||
                ts == TypeSignature.Int16 ||
                ts == TypeSignature.UInt16 ||
                ts == TypeSignature.Int32 ||
                ts == TypeSignature.UInt32 ||
                ts == TypeSignature.Int64 ||
                ts == TypeSignature.UInt64 ||
                ts == TypeSignature.IntPtr ||
                ts == TypeSignature.UIntPtr ||
                ts == TypeSignature.Single ||
                ts == TypeSignature.Double;

            bool isNumericType(TypeReference t) =>
                t.Match(
                    s => isNumericSignature(s.Type),
                    array => false,
                    byRef => false,
                    ptr => true,
                    generic => true,
                    func => false);

            var from = n.Value.Type();
            var to = n.Type;

            if (!isNumericType(from))
                e.AddErr($"Can not apply numeric conversion on expression of type {from}", "value"); // TODO: expression type validation path
            if (!isNumericType(to))
                e.AddErr($"Can not use numeric conversion to convert to type {to}", "type");
        }
    }
}
