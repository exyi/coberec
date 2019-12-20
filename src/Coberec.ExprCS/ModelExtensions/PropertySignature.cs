using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Basic metadata about a property - <see cref="Name">, <see cref="Accessibility" />, <see cref="DeclaringType" />, ... </summary>
    public partial class PropertySignature
    {
        public static PropertySignature Create(string name, TypeSignature declaringType, TypeReference type, Accessibility getter, Accessibility setter, bool isStatic = false, bool isVirtual = false, bool isOverride = false, bool isAbstract = false)
        {
            if (getter == null && setter == null) throw new ArgumentNullException(nameof(getter), "Property must have getter or setter.");

            var getMethod = getter?.Apply(a => new MethodSignature(declaringType, ImmutableArray<MethodParameter>.Empty, "get_" + name, type, isStatic, a, isVirtual, isOverride, isAbstract, true, ImmutableArray<GenericParameter>.Empty));
            var setMethod = setter?.Apply(a => new MethodSignature(declaringType, ImmutableArray.Create(new MethodParameter(type, "value")), "set_" + name, TypeSignature.Void, isStatic, a, isVirtual, isOverride, isAbstract, true, ImmutableArray<GenericParameter>.Empty));

            return new PropertySignature(declaringType, type, name, Accessibility.Max(getter, setter), isStatic, getMethod, setMethod);
        }

        public bool IsOverride => (Getter ?? Setter).IsOverride;


        /// <summary> Fills in the generic parameters. </summary>
        public PropertyReference Specialize(IEnumerable<TypeReference> typeArgs) =>
            new PropertyReference(this, typeArgs.ToImmutableArray());

        /// <summary> Fills in the generic parameters from the declaring type. Useful when using the property inside it's declaring type. </summary>
        public PropertyReference SpecializeFromDeclaringType() =>
            new PropertyReference(this, this.DeclaringType.TypeParameters.EagerSelect(TypeReference.GenericParameter));

        public static implicit operator PropertyReference(PropertySignature signature)
        {
            Assert.Empty(signature.DeclaringType.TypeParameters);
            return new PropertyReference(signature, ImmutableArray<TypeReference>.Empty);
        }

        // public static PropertySignature FromReflection(System.Reflection.PropertyInfo property)
        // {
        //     var declaringType = TypeSignature.FromType(property.DeclaringType);
        //     var accessibility = property. || property.IsNestedPublic ? Accessibility.APublic :
        //                         type.IsNestedAssembly ? Accessibility.AInternal :
        //                         type.IsNestedPrivate ? Accessibility.APrivate :
        //                         type.IsNestedFamily ? Accessibility.AProtected :
        //                         type.IsNestedFamORAssem ? Accessibility.AProtectedInternal :
        //                         type.IsNestedFamANDAssem ? Accessibility.APrivateProtected :
        //                         throw new NotSupportedException("Unsupported accesibility of "+ type);
        //     return new PropertySignature(declaringType, TypeReference.FromType(property.PropertyType), property.Name, Accessibility.ty
        // }
    }
}
