using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;

namespace Coberec.ExprCS
{
    public partial class PropertySignature
    {
        public static PropertySignature Create(string name, TypeSignature declaringType, TypeReference type, Accessibility getter, Accessibility setter, bool isStatic = false, bool isVirtual = false, bool isOverride = false, bool isAbstract = false)
        {
            if (getter == null && setter == null) throw new ArgumentNullException(nameof(getter), "Property must have getter or setter.");

            var getMethod = getter?.Apply(a => new MethodSignature(declaringType, ImmutableArray<MethodParameter>.Empty, "get_" + name, type, isStatic, a, isVirtual, isOverride, isAbstract, true, ImmutableArray<GenericParameter>.Empty));
            var setMethod = setter?.Apply(a => new MethodSignature(declaringType, ImmutableArray.Create(new MethodParameter(type, "value")), "set_" + name, TypeSignature.Void, isStatic, a, isVirtual, isOverride, isAbstract, true, ImmutableArray<GenericParameter>.Empty));

            return new PropertySignature(declaringType, type, name, Accessibility.Max(getter, setter), isStatic, getMethod, setMethod);
        }
    }
}
