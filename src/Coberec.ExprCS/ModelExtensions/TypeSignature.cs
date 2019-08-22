using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class TypeSignature
    {
        public static TypeSignature Void = new TypeSignature("Void", NamespaceSignature.System, true, false, Accessibility.APublic, 0);
        public static TypeSignature Int32 = new TypeSignature("Int32", NamespaceSignature.System, true, false, Accessibility.APublic, 0);
        public static TypeSignature TimeSpan = new TypeSignature("TimeSpan", NamespaceSignature.System, true, false, Accessibility.APublic, 0);
        public static TypeSignature Object = new TypeSignature("Object", NamespaceSignature.System, true, false, Accessibility.APublic, 0);
        public static TypeSignature Boolean = new TypeSignature("Boolean", NamespaceSignature.System, true, false, Accessibility.APublic, 0);
        public static TypeSignature String = new TypeSignature("String", NamespaceSignature.System, true, false, Accessibility.APublic, 0);
    }
}
