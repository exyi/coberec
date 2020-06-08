using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;

namespace Coberec.ExprCS
{
    static class ImplementationResolver
    {
        public static TypeSignature UnwrapSignature(SpecializedType st) =>
            st.TypeArguments.Length == 0 ? st.Type : throw new NotSupportedException($"Generic type is not supported in this context.");

        public static TypeDef AutoResolveImplementations(TypeDef type, MetadataContext cx)
        {
            var thisType = type.Signature.SpecializeByItself();
            var baseTypes = cx.GetBaseTypes(thisType).ToArray();
            var interfaces = cx.GetDirectImplements(thisType).ToArray();

            // exclude methods and properties that are already implemented
            var explicitImplMethods = new HashSet<MethodSignature>(type.Members.OfType<MethodDef>().SelectMany(m => m.Implements).Select(m => m.Signature));
            var explicitImplProps = new HashSet<PropertySignature>(type.Members.OfType<PropertyDef>().SelectMany(m => m.Implements).Select(m => m.Signature));

            var methods = baseTypes.Concat(interfaces).ZipSelectMany(t => cx.GetMemberMethodDefs(t.Type).Where(m => !explicitImplMethods.Contains(m))).ToLookup(m => m.Item2.Name);
            var properties = baseTypes.Concat(interfaces).ZipSelectMany(t => cx.GetMemberPropertyDefs(t.Type).Where(p => !explicitImplProps.Contains(p))).ToLookup(p => p.Item2.Name);

            var myMembers = type.Members.Select(member => {
                // implementations must be public
                if (member is MethodDef method && methods.Contains(method.Signature.Name))
                {
                    var mm = methods[method.Signature.Name]
                             .Where(m2 => m2.Item2.Params.Select(p => p.Type).SequenceEqual(method.Signature.Params.Select(p => p.Type)))
                             .Where(m2 => m2.Item2.ResultType == method.Signature.ResultType)
                             .Where(m2 => method.Signature.Accessibility == m2.Item2.Accessibility)
                             .Where(m2 => method.Signature.IsOverride || m2.Item2.DeclaringType.Kind == "interface")
                             .ToArray();
                    return method.With(implements: method.Implements.AddRange(mm.Select(m => m.Item2.Specialize(m.Item1.TypeArguments, m.Item2.TypeParameters.Select(TypeReference.GenericParameter)))));
                }
                else if (member is PropertyDef property && properties.Contains(property.Signature.Name))
                {
                    var pp = properties[property.Signature.Name]
                             .Where(p2 => p2.Item2.Type == property.Signature.Type)
                             .Where(p2 => property.Signature.Accessibility == p2.Item2.Accessibility)
                             .Where(p2 => property.Signature.IsOverride || p2.Item2.DeclaringType.Kind == "interface")
                             .ToArray();
                    return property.With(implements: property.Implements.AddRange(pp.Select(p => p.Item2.Specialize(p.Item1.TypeArguments))));
                }
                else return member;
            }).ToImmutableArray();
            return type.With(members: myMembers);
        }
    }
}
