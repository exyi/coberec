using System;
using Coberec.CoreLib;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class NamespaceSignature
    {
        public override string ToString() =>
            Parent == Global || Parent == null ? Name :
            $"{Parent}.{Name}";
        /// <summary> The global namespace that contains everything. This is the only case when NamespaceSignature has Parent=null </summary>
        public static NamespaceSignature Global = new NamespaceSignature("", null);
        public static NamespaceSignature System = new NamespaceSignature("System", Global);

        public static NamespaceSignature Parse(string ns) =>
            Create(ns.Split('.'));

        public static NamespaceSignature Create(Span<string> nsParts) =>
            nsParts.IsEmpty ? Global :
            new NamespaceSignature(nsParts[nsParts.Length - 1], Create(nsParts.Slice(0, nsParts.Length - 1)));
    }
}
