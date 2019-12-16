using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using Coberec.CSharpGen.TypeSystem;

namespace Coberec.ExprCS
{
    public static class SymbolNamer
    {
        public static string NameType(string @namespace, string desiredName, int genericArgCount, ICompilation compilation, IEnumerable<string> blacklist = null)
        {
            desiredName = NameSanitizer.SanitizeTypeName(desiredName);
            var ns = compilation.RootNamespace.FindDescendantNamespace(@namespace);

            if (ns == null) return desiredName; // there will be no collision

            var existingNames = new HashSet<string>(ns.Types.Where(t => t.TypeArguments.Count == genericArgCount).Select(t => t.Name).Concat(ns.ChildNamespaces.Select(t => t.Name)), StringComparer.InvariantCultureIgnoreCase);
            existingNames.UnionWith(blacklist ?? Enumerable.Empty<string>());

            if (!existingNames.Contains(desiredName)) return desiredName;
            for (int i = 2; true; i++)
            {
                if (!existingNames.Contains(desiredName + i)) return desiredName + i;
            }
        }

        public static string NameMember(ITypeDefinition type, string desiredName, bool? lowerCase) =>
            NameMember(type, new Dictionary<MemberSignature, string>(), desiredName, lowerCase);

        public static string NameMethod(ITypeDefinition type, string desiredName, int typeArgCount, IEnumerable<IParameter> signature, bool isOverride, bool? lowerCase = false) =>
            NameMethod(type, desiredName, typeArgCount, signature.Select(a => a.Type).ToArray(), isOverride, lowerCase);
        public static string NameMethod(ITypeDefinition type, string desiredName, int typeArgCount, IType[] signature, bool isOverride, bool? lowerCase = false) =>
            NameMethod(type, new Dictionary<MemberSignature, string>(), desiredName, typeArgCount, signature.Select(SymbolLoader.TypeRef).ToArray(), isOverride, lowerCase);

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

        public static bool IsSpecial(MemberSignature m) => (m as MethodSignature)?.HasSpecialName == true || m.Name.StartsWith("<");

        static string NameMember(ITypeDefinition type, Dictionary<MemberSignature, string> names, string desiredName, bool? lowerCase, IEnumerable<string> blacklist = null)
        {
            desiredName = NameSanitizer.SanitizeMemberName(desiredName, lowerCase);

            var existingNames = new HashSet<string>(type.GetMembers(m => m.DeclaringTypeDefinition == type || m.IsVirtual).Select(m => m.Name));
            existingNames.UnionWith(type.NestedTypes.Select(t => t.Name));
            existingNames.UnionWith(names.Values);
            existingNames.UnionWith(blacklist ?? Enumerable.Empty<string>());
            for (IType p = type; p != null; p = p.DeclaringType)
                existingNames.Add(p.Name);

            if (!existingNames.Contains(desiredName)) return desiredName;
            for (int i = 2; true; i++)
            {
                if (!existingNames.Contains(desiredName + i)) return desiredName + i;
            }
        }

        static string NameMethod(ITypeDefinition type, Dictionary<MemberSignature, string> names, string desiredName, int typeArgCount, TypeReference[] signature, bool isOverride, bool? lowerCase = false)
        {
            desiredName = NameSanitizer.SanitizeMemberName(desiredName, lowerCase);

            var existingNonmethods = new HashSet<string>(type.GetMembers(m => m.SymbolKind != SymbolKind.Method && (m.DeclaringTypeDefinition == type || m.IsVirtual)).Select(m => m.Name));
            for (IType p = type; p != null; p = p.DeclaringType)
                existingNonmethods.Add(p.Name);
            existingNonmethods.UnionWith(names.Where(n => !(n.Key is MethodSignature)).Select(n => n.Value));

            var existingMethods =
                type
                .GetMembers(m => m.SymbolKind == SymbolKind.Method && (m.DeclaringTypeDefinition == type || m.IsVirtual && !isOverride))
                .Cast<IMethod>()
                .Select(SymbolLoader.Method)
                .Concat(names.Where(m => m.Key is MethodSignature).Select(m => ((MethodSignature)m.Key).With(name: m.Value)))
                .ToLookup(m => m.Name);

            bool collides(string n)
            {
                if (existingNonmethods.Contains(n)) return true;
                if (!existingMethods.Contains(n)) return false;
                var methods = existingMethods[n];
                return methods.Any(m => m.TypeParameters.Length == typeArgCount && m.Params.Length == signature.Length && m.Params.Select(p => p.Type).SequenceEqual(signature));
            }

            if (!collides(desiredName)) return desiredName;
            for (int i = 2; true; i++)
            {
                if (!collides(desiredName + i)) return desiredName + i;
            }
        }

        public static IEnumerable<string> BlacklistedTypeNames(TypeDef typeDef)
        {
            // TODO: renamed virtual methods
            return typeDef.Members.OfType<MethodDef>().Where(m => m.Signature.IsOverride || m.Signature.IsVirtual).AsEnumerable<MemberDef>()
                   .Concat(typeDef.Members.OfType<PropertyDef>().Where(m => m.Signature.IsOverride || m.Signature.Getter?.IsVirtual == true))
                   .Select(m => m.Signature.Name)
                   .Concat(new [] { "Finalize", "GetType", "ToString", "Equals", "ReferenceEquals", "GetHashCode", "MemberwiseClone" });
        }

        public static Dictionary<MemberSignature, string> NameMembers(TypeDef type, VirtualType realType, bool adjustCasing)
        {
            var result = new Dictionary<MemberSignature, string>();

            // TODO: overrides
            // foreach (var m in type.Members.OfType<MethodDef>().Where(m => m.Signature.IsOverride))
            // {
            //     var overridesName = m.Implements.Where(i => i.DeclaringType.Kind == "class").Select(cx.GetMethod).SingleOrDefault()?.Name;

            // }

            foreach (var m in type.Members
                .Where(m => !IsSpecial(m.Signature))
                .OrderBy(m =>
                              m.Signature.Accessibility == Accessibility.APrivate ? 20 : // private
                              m.Signature.Accessibility == Accessibility.AInternal || m.Signature.Accessibility == Accessibility.APrivateProtected ? 19 : // internal
                              m.Signature.Accessibility == Accessibility.APublic ? 0 : // public - top priority
                              10 // protected (so kindof public)
                        ))
            {
                if (m is MethodDef method)
                {
                    var lowerCase = adjustCasing ? false : (bool?)null;
                    var name = NameMethod(realType, result, m.Signature.Name, method.Signature.TypeParameters.Length, method.Signature.Params.Select(p => p.Type).ToArray(), method.Signature.IsOverride, lowerCase);
                    result.Add(method.Signature, name);
                }
                else
                {
                    var blacklist = m is TypeDef typeMember ? BlacklistedTypeNames(typeMember) : null;
                    var lowerCase = adjustCasing ? m is FieldDef : (bool?)null;
                    var name = NameMember(realType, result, m.Signature.Name, lowerCase, blacklist);
                    result.Add(m.Signature, name);
                }
            }

            foreach (var m in type.Members.Where(m => IsSpecial(m.Signature)))
            {
                // don't sanitize nor avoid collisions
                // just replace <oldName> with <newName>
                var name = m.Signature.Name;
                foreach (var m2 in type.Members)
                {
                    if (!IsSpecial(m2.Signature))
                        name = name.Replace("<" + m2.Signature.Name + ">", "<" + result[m2.Signature] + ">");
                }
                result.Add(m.Signature, name);
            }

            return result;
        }

        public static string NameType(TypeDef type, ICompilation compilation)
        {
            var blacklist = BlacklistedTypeNames(type);
            var ns = ((TypeOrNamespace.NamespaceSignatureCase)type.Signature.Parent).Item.ToString();
            return NameType(ns, type.Signature.Name, type.Signature.TypeParameters.Length, compilation, blacklist);
        }
    }
}
