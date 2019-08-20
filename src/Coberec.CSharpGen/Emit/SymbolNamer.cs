using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using Coberec.CSharpGen.TypeSystem;

namespace Coberec.CSharpGen.Emit
{
    public static class SymbolNamer
    {
        public static string NameType(string @namespace, string desiredName, EmitContext cx)
        {
            desiredName = NameSanitizer.SanitizeTypeName(desiredName);
            var ns = cx.Compilation.RootNamespace.FindDescendantNamespace(@namespace);

            if (ns == null) return desiredName; // there will be no collision

            var existingNames = new HashSet<string>(ns.Types.Select(t => t.Name).Concat(ns.ChildNamespaces.Select(t => t.Name)), StringComparer.InvariantCultureIgnoreCase);

            if (!existingNames.Contains(desiredName)) return desiredName;
            for (int i = 2; true; i++)
            {
                if (!existingNames.Contains(desiredName + i)) return desiredName + i;
            }
            throw new Exception("wtf");
        }

        public static string NameMember(ITypeDefinition type, string desiredName, bool? lowerCase)
        {
            desiredName = NameSanitizer.SanitizeMemberName(desiredName, lowerCase);
            var existingNames = new HashSet<string>(type.GetMembers().Select(m => m.Name));
            existingNames.UnionWith(type.NestedTypes.Select(t => t.Name));
            for (IType p = type; p != null; p = p.DeclaringType)
                existingNames.Add(p.Name);

            if (!existingNames.Contains(desiredName)) return desiredName;
            for (int i = 2; true; i++)
            {
                if (!existingNames.Contains(desiredName + i)) return desiredName + i;
            }
            throw new Exception("wtf");
        }

        public static string NameMethod(ITypeDefinition type, string desiredName, int typeArgCount, IEnumerable<IParameter> signature, bool? lowerCase = false) =>
            NameMethod(type, desiredName, typeArgCount, signature.Select(a => a.Type).ToArray(), lowerCase);
        public static string NameMethod(ITypeDefinition type, string desiredName, int typeArgCount, IType[] signature, bool? lowerCase = false)
        {
            desiredName = NameSanitizer.SanitizeMemberName(desiredName, lowerCase);

            var existingNonmethods = new HashSet<string>(type.GetMembers(m => m.SymbolKind != SymbolKind.Method).Select(m => m.Name));
            for (IType p = type; p != null; p = p.DeclaringType)
                existingNonmethods.Add(p.Name);
            var existingMethods = type.GetMembers(m => m.SymbolKind == SymbolKind.Method).Cast<IMethod>().ToLookup(m => m.Name);

            bool collides(string n)
            {
                if (existingNonmethods.Contains(n)) return true;
                if (!existingMethods.Contains(n)) return false;
                var methods = existingMethods[n];
                return methods.Any(m => m.TypeArguments.Count == typeArgCount && m.Parameters.Count == signature.Length && m.Parameters.Select(p => p.Type.FullName).SequenceEqual(signature.Select(s => s.FullName)));
            }

            if (!collides(desiredName)) return desiredName;
            for (int i = 2; true; i++)
            {
                if (!collides(desiredName + i)) return desiredName + i;
            }
            throw new Exception("wtf");
        }

        public static IParameter[] NameParameters(IEnumerable<IParameter> parameters)
        {
            // Except for sanitization and lowercasing, I'm not aware of any restrictions

            var usedNames = new HashSet<string>();
            return parameters.Select(p => {
                var name = NameSanitizer.SanitizeCsharpName(p.Name, lowerCase: true);
                string name2 = name;
                int i = 2;
                while(!usedNames.Add(name2)) name2 = name + (i++);
                return (IParameter)new VirtualParameter(p.Type, name2, p.Owner, p.GetAttributes().ToArray(), p.ReferenceKind, p.IsRef, p.IsOut, p.IsIn, p.IsParams, p.IsOptional, p.GetConstantValue());
            }).ToArray();
        }
    }
}
