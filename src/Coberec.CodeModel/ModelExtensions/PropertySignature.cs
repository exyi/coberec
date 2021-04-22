using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using Coberec.Utils;
using Xunit;
using R = System.Reflection;

namespace Coberec.ExprCS
{
    public partial class PropertySignature
    {
        static partial void ValidateObjectExtension(ref ValidationErrorsBuilder e, PropertySignature p)
        {
            if (p.Getter is null && p.Setter is null)
            {
                e.AddErr("Getter or setter must specified", "getter");
                e.AddErr("Setter or getter must specified", "setter");
            }
            if (p.Getter is object)
            {
                if (!p.Getter.Params.IsEmpty)
                    e.AddErr("Getter must have no parameters", "getter", "params");
            }
        }

        /// <summary> Signature of <see cref="Nullable{T}.HasValue" /> </summary>
        public static readonly PropertySignature Nullable_HasValue =
            PropertyReference.FromLambda<int?>(a => a.HasValue).Signature;
        /// <summary> Signature of <see cref="Nullable{T}.Value" /> </summary>
        public static readonly PropertySignature Nullable_Value =
            PropertyReference.FromLambda<int?>(a => a.Value).Signature;

        public static PropertySignature Create(string name, TypeSignature declaringType, TypeReference type, Accessibility getter, Accessibility setter, bool isStatic = false, bool isVirtual = false, bool isOverride = false, bool isAbstract = false)
        {
            var getMethod = getter?.Apply(a => new MethodSignature(declaringType, ImmutableArray<MethodParameter>.Empty, "get_" + name, type, isStatic, a, isVirtual, isOverride, isAbstract, true, ImmutableArray<GenericParameter>.Empty));
            var setMethod = setter?.Apply(a => new MethodSignature(declaringType, ImmutableArray.Create(new MethodParameter(type, "value")), "set_" + name, TypeSignature.Void, isStatic, a, isVirtual, isOverride, isAbstract, true, ImmutableArray<GenericParameter>.Empty));

            return new PropertySignature(declaringType, type, name, Accessibility.Max(getter, setter), isStatic, getMethod, setMethod);
        }

        /// <summary> Creates a new property signature of an abstract property. </summary>
        public static PropertySignature Abstract(string name, TypeSignature declaringType, TypeReference type, Accessibility getter, Accessibility setter = null, bool isOverride = false) =>
            Create(name, declaringType, type, getter, setter, isOverride: isOverride, isVirtual: true, isAbstract: true);

        /// <summary> Creates a new property signature of a static property. </summary>
        public static PropertySignature Static(string name, TypeSignature declaringType, TypeReference type, Accessibility getter, Accessibility setter = null) =>
            Create(name, declaringType, type, getter, setter, isStatic: true);

        /// <summary> Creates a new property signature of an instance property. </summary>
        public static PropertySignature Instance(string name, TypeSignature declaringType, TypeReference type, Accessibility getter, Accessibility setter = null, bool isVirtual = false, bool isOverride = false) =>
            Create(name, declaringType, type, getter, setter, isOverride: isOverride, isVirtual: isVirtual, isAbstract: false);

        /// <summary> Declares a property that overrides the <paramref name="overriddenProperty" /> in the specified declaring type. The property must be virtual or from an interface. </summary>
        public static PropertySignature Override(TypeSignature declaringType, PropertySignature overriddenProperty, OptParam<bool> isVirtual = default, bool isAbstract = false)
        {
            var isInterface = overriddenProperty.DeclaringType.Kind == "interface";
            if (!isInterface && !overriddenProperty.IsVirtual)
                throw new ArgumentException($"Can't override non-virtual property {overriddenProperty}");

            return Create(overriddenProperty.Name,
                          declaringType,
                          overriddenProperty.Type,
                          overriddenProperty.Getter?.Accessibility,
                          overriddenProperty.Setter?.Accessibility,
                          isVirtual: isVirtual.ValueOrDefault(!isInterface && declaringType.CanOverride),
                          isOverride: !isInterface,
                          isAbstract: isAbstract);

        }

        public bool IsOverride => (Getter ?? Setter).IsOverride;
        public bool IsVirtual => (Getter ?? Setter).IsVirtual;


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

        public FmtToken Format() =>
            Format(this, this.Type);

        internal static string Format(PropertySignature s, TypeReference resultType)
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

        /// <summary> Creates a PropertySignature of a property represented by System.Reflection type </summary>
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
