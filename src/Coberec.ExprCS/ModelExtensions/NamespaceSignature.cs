using System;
using Coberec.CoreLib;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class NamespaceSignature
    {
        static partial void ValidateObjectExtension(ref ValidationErrorsBuilder e, NamespaceSignature obj)
        {
            if (obj.Parent is null && !string.IsNullOrEmpty(obj.Name))
                e.Add(ValidationErrors.Create($"Namespace {obj.Name} must have a parent (did you intent to use NamespaceSignature.Global as a parent?)").Nest("parent"));
            if (string.IsNullOrEmpty(obj.Name) && obj.Parent is object)
                e.Add(ValidationErrors.Create($"Namespace in {obj.Parent} must have a non-empty name.").Nest("name"));
        }

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
            nsParts.Length == 1 && nsParts[0] == "System" ? System :
            new NamespaceSignature(nsParts[nsParts.Length - 1], Create(nsParts.Slice(0, nsParts.Length - 1)));
    }
}
