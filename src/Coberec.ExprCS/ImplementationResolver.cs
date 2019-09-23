using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
    static class ImplementationResolver
    {
        public static TypeSignature UnwrapSignature(SpecializedType st) =>
            st.GenericParameters.Length == 0 ? st.Type : throw new NotSupportedException($"Generic type is not supported in this context.");

        public static TypeDef AutoResolveImplementations(TypeDef type, MetadataContext cx)
        {
            var baseTypes = cx.GetBaseTypes(type.Signature).ToArray();
            var interfaces = cx.GetDirectImplements(type.Signature).ToArray();

            // exclude method and properties that are already implemented
            var explicitImplMethods = new HashSet<MethodSignature>(type.Members.OfType<MethodDef>().SelectMany(m => m.Implements));
            var explicitImplProps = new HashSet<PropertySignature>(type.Members.OfType<PropertyDef>().SelectMany(m => m.Implements));

            var methods = baseTypes.Concat(interfaces).Select(UnwrapSignature).SelectMany(cx.GetMemberMethods).Where(m => !explicitImplMethods.Contains(m)).ToLookup(m => m.Name);
            var properties = baseTypes.Concat(interfaces).Select(UnwrapSignature).SelectMany(cx.GetMemberProperties).Where(p => !explicitImplProps.Contains(p)).ToLookup(p => p.Name);


            var myMembers = type.Members.Select(member => {
                // implementations must be public
                if (member is MethodDef method && methods.Contains(method.Signature.Name))
                {
                    var mm = methods[method.Signature.Name]
                             .Where(m2 => m2.Params.Select(p => p.Type).SequenceEqual(method.Signature.Params.Select(p => p.Type)))
                             .Where(m2 => m2.ResultType == method.Signature.ResultType)
                             .Where(m2 => method.Signature.Accessibility == m2.Accessibility)
                             .Where(m2 => method.Signature.IsOverride || m2.DeclaringType.Kind == "interface")
                             .ToArray();
                    return method.With(implements: method.Implements.AddRange(mm));
                }
                else if (member is PropertyDef property && properties.Contains(property.Signature.Name))
                {
                    var pp = properties[property.Signature.Name]
                             .Where(p2 => p2.Type == property.Signature.Type)
                             .Where(p2 => property.Signature.Accessibility == p2.Accessibility)
                             .Where(p2 => property.Signature.IsOverride || p2.DeclaringType.Kind == "interface")
                             .ToArray();
                    return property.With(implements: property.Implements.AddRange(pp));
                }
                else return member;
            }).ToImmutableArray();
            return type.With(members: myMembers);
        }
    }
}
