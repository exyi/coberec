using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;
using Xunit;
using R = System.Reflection;

namespace Coberec.ExprCS
{
    /// <summary> Basic metadata about a property - <see cref="Name" />, <see cref="Accessibility" />, <see cref="DeclaringType" />, ... </summary>
    public partial class PropertySignature
    {
        public static readonly PropertySignature Nullable_HasValue =
            PropertyReference.FromLambda<int?>(a => a.HasValue).Signature;
        public static readonly PropertySignature Nullable_Value =
            PropertyReference.FromLambda<int?>(a => a.Value).Signature;

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

        /// <summary> Fills in the generic parameters. </summary>
        public PropertyReference Specialize(params TypeReference[] typeArgs) => Specialize(typeArgs.AsEnumerable());

        /// <summary> Fills in the generic parameters from the declaring type. Useful when using the property inside it's declaring type. </summary>
        public PropertyReference SpecializeFromDeclaringType() =>
            new PropertyReference(this, this.DeclaringType.AllTypeParameters().EagerSelect(TypeReference.GenericParameter));

        public static implicit operator PropertyReference(PropertySignature signature)
        {
            Assert.Empty(signature.DeclaringType.TypeParameters);
            return new PropertyReference(signature, ImmutableArray<TypeReference>.Empty);
        }

        public override string ToString() =>
            ToString(this, this.Type);

        internal static string ToString(PropertySignature s, TypeReference resultType)
        {
            var sb = new System.Text.StringBuilder();
            if (s.Accessibility != Accessibility.APublic) sb.Append(s.Accessibility).Append(" ");
            if (s.IsStatic) sb.Append("static ");
            var m = s.Getter ?? s.Setter;
            if (m.IsVirtual && !m.IsOverride) sb.Append("virtual ");
            if (m.IsOverride && !m.IsVirtual) sb.Append("sealed ");
            if (m.IsOverride) sb.Append("override ");
            if (m.IsAbstract) sb.Append("abstract ");
            sb.Append(s.Name);
            sb.Append(" { ");
            if (s.Getter is object)
            {
                if (s.Getter.Accessibility != s.Accessibility)
                    sb.Append(s.Getter.Accessibility).Append(" ");
                sb.Append("get; ");
            }
            if (s.Setter is object)
            {
                if (s.Setter.Accessibility != s.Accessibility)
                    sb.Append(s.Setter.Accessibility).Append(" ");
                sb.Append("set; ");
            }
            sb.Append("}: ");
            sb.Append(resultType);
            return sb.ToString();
        }

        public static PropertySignature FromReflection(R.PropertyInfo prop)
        {
            prop = MethodSignature.SanitizeDeclaringTypeGenerics(prop);
            var declaringType = TypeSignature.FromType(prop.DeclaringType);
            var get = prop.GetMethod?.Apply(MethodSignature.FromReflection);
            var set = prop.SetMethod?.Apply(MethodSignature.FromReflection);
            var m = get ?? set;

            var resultType = TypeReference.FromType(prop.PropertyType);
            return PropertySignature.Create(prop.Name,
                                            declaringType,
                                            resultType,
                                            get?.Accessibility,
                                            set?.Accessibility,
                                            m.IsStatic,
                                            m.IsVirtual,
                                            m.IsOverride,
                                            m.IsAbstract);
        }
    }
}
