using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;
using Xunit;

namespace Coberec.ExprCS.CodeTranslation
{
    static class ILAstFactory
    {
        public static ILInstruction Constant(object constant, IType type)
        {
            if (constant == null)
            {
                Assert.True(type.IsReferenceType);
                return new LdNull();
            }
            else if (constant is bool boolC)
            {
                Assert.Equal(typeof(bool).FullName, type.FullName);
                return new LdcI4(boolC ? 1 : 0);
            }
            else if (constant is char || constant is byte || constant is sbyte || constant is ushort || constant is short || constant is int)
            {
                return new LdcI4(Convert.ToInt32(constant));
            }
            else if (constant is uint uintC)
            {
                return new LdcI4(unchecked((int)uintC));
            }
            else if (constant is long longC)
                return new LdcI8(longC);
            else if (constant is ulong ulongC)
                return new LdcI8(unchecked((long)ulongC));
            else if (constant is float floatC)
                return new LdcF4(floatC);
            else if (constant is double doubleC)
                return new LdcF8(doubleC);
            else if (constant is decimal decimalC)
                return new LdcDecimal(decimalC);
            else if (constant is string stringC)
                return new LdStr(stringC);
            else
                throw new NotSupportedException($"Constant '{constant}' of type '{constant.GetType()}' with declared type '{type}' is not supported.");
        }

        public static ILInstruction FieldAddr(IField field, ILVariable target) =>
            target is object ?
            new LdFlda(new LdLoc(target), field) :
            (ILInstruction)new LdsFlda(field);
    }
}
