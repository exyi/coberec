using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using Coberec.CSharpGen;
using Xunit;
using R = System.Reflection;

namespace Coberec.ExprCS
{
    public partial class MethodSignature
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, MethodSignature m)
        {
            if (m.IsConstructor() && m.IsStatic)
                e.Add(ValidationErrors.Create($"Constructor '{m}' can't be static").Nest("isStatic"));
            if (m.IsStaticConstructor() && !m.IsStatic)
                e.Add(ValidationErrors.Create($"Static constructor '{m}' must be static").Nest("isStatic"));

            if (m.IsAbstract && m.DeclaringType?.IsAbstract == false)
                e.Add(ValidationErrors.Create($"Can not declare abstract method in {m.DeclaringType}").Nest("isAbstract"));
            if (m.IsAbstract && !m.IsVirtual)
                e.Add(ValidationErrors.Create($"Can not declare abstract method that is not virtual").Nest("isVirtual"));
            if (m.Accessibility == Accessibility.APrivate && (m.IsVirtual || m.IsOverride))
                e.Add(ValidationErrors.Create($"Can not declare virtual or override method that is private").Nest("accessibility"));
            if (m.IsStatic)
            {
                if (m.IsVirtual) e.Add(ValidationErrors.Create($"Can not declare virtual static method").Nest("isVirtual"));
                if (m.IsOverride) e.Add(ValidationErrors.Create($"Can not declare override static method").Nest("isOverride"));
                if (m.IsAbstract) e.Add(ValidationErrors.Create($"Can not declare abstract static method").Nest("isAbstract"));
            }
            if (!m.DeclaringType.CanOverride)
            {
                if (m.IsVirtual) e.Add(ValidationErrors.Create($"Can not declare virtual method in {m.DeclaringType}").Nest("isVirtual"));
                // if (m.IsOverride) e.Add(ValidationErrors.Create($"Can not declare override method in {m.DeclaringType}").Nest("isOverride"));
                if (m.IsAbstract) e.Add(ValidationErrors.Create($"Can not declare abstract method in {m.DeclaringType}").Nest("isAbstract"));
                if (m.DeclaringType.IsAbstract && !m.IsStatic)
                    e.Add(ValidationErrors.Create($"Can not declare non-stattic method in {m.DeclaringType}").Nest("isStatic"));
            }
        }

        /// <summary> Signature of method <see cref="Object.ToString" /> </summary>
        public static readonly MethodSignature Object_ToString = MethodReference.FromLambda<object>(a => a.ToString()).Signature;
        /// <summary> Signature of method <see cref="Object.GetType" /> </summary>
        public static readonly MethodSignature Object_GetType = MethodReference.FromLambda<object>(a => a.GetType()).Signature;
        /// <summary> Signature of method <see cref="Object.GetHashCode" /> </summary>
        public static readonly MethodSignature Object_GetHashCode = MethodReference.FromLambda<object>(a => a.GetHashCode()).Signature;
        /// <summary> Signature of method <see cref="Object.Equals(object)" /> </summary>
        public static readonly MethodSignature Object_Equals = MethodReference.FromLambda<object>(a => a.Equals(null)).Signature;
        /// <summary> Signature of constructor <see cref="Object.Object" /> </summary>
        public static readonly MethodSignature Object_Constructor = MethodReference.FromLambda(() => new object()).Signature;
        /// <summary> Signature of constructor <see cref="Nullable{T}.Nullable(T)" /> </summary>
        public static readonly MethodSignature NullableOfT_Constructor = MethodReference.FromLambda(() => new int?(34)).Signature;

        /// <summary> Gets signature of public parameterless constructor. Note that this method does not check if it actually exists, so the compilation may fail in later phase. </summary>
        public static MethodSignature ImplicitConstructor(TypeSignature declaringType) =>
            Constructor(declaringType, declaringType.IsAbstract ? Accessibility.AProtected : Accessibility.APublic);

        /// <summary> Creates new method signature that is a constructor </summary>
        public static MethodSignature Constructor(TypeSignature declaringType, Accessibility accessibility, params MethodParameter[] parameters) =>
            Constructor(declaringType, accessibility, parameters.AsEnumerable());
        /// <summary> Creates new method signature that is a constructor </summary>
        public static MethodSignature Constructor(TypeSignature declaringType, Accessibility accessibility, IEnumerable<MethodParameter> parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), ".ctor", TypeSignature.Void, isStatic: false, accessibility, isVirtual: false, isOverride: false, isAbstract: false, hasSpecialName: true, ImmutableArray<GenericParameter>.Empty);

        /// <summary> Creates new static method signature </summary>
        public static MethodSignature Static(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, params MethodParameter[] parameters) =>
            Static(name, declaringType, accessibility, returnType, null, parameters);
        /// <summary> Creates new static method signature </summary>
        public static MethodSignature Static(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, IEnumerable<GenericParameter> genericParameters, params MethodParameter[] parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: true, accessibility, isVirtual: false, isOverride: false, isAbstract: false, hasSpecialName: false, genericParameters?.ToImmutableArray() ?? ImmutableArray<GenericParameter>.Empty);

        /// <summary> Creates new instance method signature. The method is not override, not virtual, not abstract </summary>
        public static MethodSignature Instance(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, params MethodParameter[] parameters) =>
            Instance(name, declaringType, accessibility, returnType, ImmutableArray<GenericParameter>.Empty, parameters);
        /// <summary> Creates new instance method signature. The method is not override, not virtual, not abstract </summary>
        public static MethodSignature Instance(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, IEnumerable<GenericParameter> typeParameters, params MethodParameter[] parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: false, accessibility, isVirtual: false, isOverride: false, isAbstract: false, hasSpecialName: false, typeParameters.ToImmutableArray());

        /// <summary> Creates new instance abstract method signature. It is not override, but you can apply `.With(isOverride: true)` to the result to make it. </summary>
        public static MethodSignature Abstract(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, params MethodParameter[] parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: false, accessibility, isVirtual: true, isOverride: false, isAbstract: true, hasSpecialName: false, ImmutableArray<GenericParameter>.Empty);

        /// <summary> Creates new instance abstract method signature. It is not override, but you can apply `.With(isOverride: true)` to the result to make it. </summary>
        public static MethodSignature Abstract(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, IEnumerable<GenericParameter> typeParameters, params MethodParameter[] parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: false, accessibility, isVirtual: true, isOverride: false, isAbstract: true, hasSpecialName: false, typeParameters.ToImmutableArray());

        /// <summary> Creates new instance virtual method signature. </summary>
        public static MethodSignature Virtual(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, params MethodParameter[] parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: false, accessibility, isVirtual: true, isOverride: false, isAbstract: false, hasSpecialName: false, ImmutableArray<GenericParameter>.Empty);

        /// <summary> Creates new instance virtual method signature. </summary>
        public static MethodSignature Virtual(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, IEnumerable<GenericParameter> typeParameters, params MethodParameter[] parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: false, accessibility, isVirtual: true, isOverride: false, isAbstract: false, hasSpecialName: false, typeParameters.ToImmutableArray());

        /// <summary> Creates new method signature with brand new type parameters. Since you can't have declared multiple methods with the same type parameters, you should use Clone to create similar method signatures. </summary>
        public MethodSignature Clone()
        {
            if (this.TypeParameters.Length == 0) return this;
            var newTypeParams = TypeParameters.EagerSelect(tp => new GenericParameter(Guid.NewGuid(), tp.Name));
            var resultType = ResultType.SubstituteGenerics(TypeParameters, newTypeParams.EagerSelect(TypeReference.GenericParameter));
            var @params = Params.EagerSelect(p => p.SubstituteGenerics(TypeParameters, newTypeParams.EagerSelect(TypeReference.GenericParameter)));
            return this.With(@params: @params, resultType: resultType, typeParameters: newTypeParams);
        }

        /// <summary> Declares a method that overrides the <paramref name="overriddenMethod" /> in the specified declaring type. The method must be virtual or from an interface. </summary>
        public static MethodSignature Override(TypeSignature declaringType, MethodSignature overriddenMethod, OptParam<bool> isVirtual = default, bool isAbstract = false)
        {
            var isInterface = overriddenMethod.DeclaringType.Kind == "interface";
            if (!isInterface && !overriddenMethod.IsVirtual)
                throw new ArgumentException($"Can't override non-virtual method {overriddenMethod}");

            overriddenMethod = overriddenMethod.Clone();
            var isVirtualP = isVirtual.ValueOrDefault(!isInterface && declaringType.CanOverride || isAbstract);
            return overriddenMethod.With(declaringType, isVirtual: isVirtualP && declaringType.CanOverride, isOverride: !isInterface, isAbstract: isAbstract);
        }

        /// <summary> Fills in the generic parameters. </summary>
        /// <param name="typeArgs">Generic arguments of the declaring type. May be null instead of empty.</param>
        /// <param name="methodArgs">Generic arguments of the method. May be null instead of empty.</param>
        public MethodReference Specialize(IEnumerable<TypeReference> typeArgs, IEnumerable<TypeReference> methodArgs) =>
            this.Specialize(typeArgs?.ToImmutableArray() ?? ImmutableArray<TypeReference>.Empty, methodArgs?.ToImmutableArray() ?? ImmutableArray<TypeReference>.Empty);

        /// <summary> Fills in the generic parameters. </summary>
        /// <param name="typeArgs">Generic arguments of the declaring type. May be null instead of empty.</param>
        /// <param name="methodArgs">Generic arguments of the method. May be null instead of empty.</param>
        public MethodReference Specialize(ImmutableArray<TypeReference> typeArgs, ImmutableArray<TypeReference> methodArgs) =>
            new MethodReference(this, typeArgs, methodArgs);

        /// <summary> Fills in the generic parameters. Both arguments for the declaring type and for the method should be specified. </summary>
        public MethodReference Specialize(params TypeReference[] allGenericArgs)
        {
            var declaringTypeParam = this.DeclaringType.TotalParameterCount();
            var typeArgs = allGenericArgs.EagerSlice(take: declaringTypeParam);
            var methodArgs = allGenericArgs.EagerSlice(skip: declaringTypeParam);
            return new MethodReference(this, typeArgs, methodArgs);
        }

        /// <summary> Fills in the generic parameters from the declaring type. Useful when using the method inside it's declaring type. </summary>
        public MethodReference SpecializeFromDeclaringType(IEnumerable<TypeReference> methodArgs) =>
            new MethodReference(this, this.DeclaringType.AllTypeParameters().EagerSelect(TypeReference.GenericParameter), methodArgs?.ToImmutableArray() ?? ImmutableArray<TypeReference>.Empty);
        /// <summary> Fills in the generic parameters from the declaring type. Useful when using the method inside it's declaring type. </summary>
        public MethodReference SpecializeFromDeclaringType(params TypeReference[] methodArgs) =>
            SpecializeFromDeclaringType(methodArgs.AsEnumerable());

        public static implicit operator MethodReference(MethodSignature signature)
        {
            if (signature == null) return null;
            Assert.Empty(signature.TypeParameters);
            Assert.Empty(signature.DeclaringType.TypeParameters);
            return new MethodReference(signature, ImmutableArray<TypeReference>.Empty, ImmutableArray<TypeReference>.Empty);
        }

        public FmtToken Format() =>
            Format(this, this.TypeParameters, this.Params, this.ResultType);

        internal static string Format(MethodSignature s, IEnumerable<object> typeArgs, ImmutableArray<MethodParameter> parameters, TypeReference resultType)
        {
            var sb = new System.Text.StringBuilder();
            if (s.Accessibility != Accessibility.APublic) sb.Append(s.Accessibility).Append(" ");
            if (s.IsStatic) sb.Append("static ");
            if (s.HasSpecialName) sb.Append("[specialname] ");
            if (s.IsVirtual && !s.IsOverride) sb.Append("virtual ");
            if (s.IsOverride && !s.IsVirtual) sb.Append("sealed ");
            if (s.IsOverride) sb.Append("override ");
            if (s.IsAbstract) sb.Append("abstract ");
            sb.Append(s.Name);
            if (!s.TypeParameters.IsEmpty)
                sb.Append("<").Append(string.Join(", ", typeArgs)).Append(">");
            sb.Append("(");
            sb.Append(string.Join(", ", parameters));
            sb.Append("): ");
            sb.Append(resultType);
            return sb.ToString();
        }

        /// <summary> Returns true if this method is a constructor (only standard instance constructor counts, for static ones see <see cref="IsStaticConstructor" /> </summary>
        public bool IsConstructor() => this.HasSpecialName && this.Name == ".ctor";
        /// <summary> Returns true if this method is a static constructor (for instance ones see <see cref="IsConstructor" /> </summary>
        public bool IsStaticConstructor() => this.HasSpecialName && this.Name == ".cctor";

        internal static T SanitizeDeclaringTypeGenerics<T>(T m)
            where T: R.MemberInfo
        {
            if (m.DeclaringType.IsGenericTypeDefinition || !m.DeclaringType.IsGenericType)
                return m;
            var d = m.DeclaringType.GetGenericTypeDefinition();
            return d.GetMembers(R.BindingFlags.DeclaredOnly | R.BindingFlags.Public | R.BindingFlags.NonPublic | R.BindingFlags.Instance | R.BindingFlags.Static).OfType<T>().Single(m2 => m2.MetadataToken == m.MetadataToken);
        }

        public static MethodSignature FromReflection(R.MethodBase method)
        {
            method = SanitizeDeclaringTypeGenerics(method.IsGenericMethod ? ((R.MethodInfo)method).GetGenericMethodDefinition() : method);
            var declaringType = TypeSignature.FromType(method.DeclaringType);
            var accessibility = method.IsPublic ? Accessibility.APublic :
                                method.IsAssembly ? Accessibility.AInternal :
                                method.IsPrivate ? Accessibility.APrivate :
                                method.IsFamily ? Accessibility.AProtected :
                                method.IsFamilyOrAssembly ? Accessibility.AProtectedInternal :
                                method.IsFamilyAndAssembly ? Accessibility.APrivateProtected :
                                throw new NotSupportedException("Unsupported accessibility of " + method);
            var parameters = method.GetParameters().EagerSelect(p => {
                var t = TypeReference.FromType(p.ParameterType);
                return new MethodParameter(t,
                                    p.Name ?? "",
                                    p.HasDefaultValue,
                                    p.HasDefaultValue ? CanonicalizeDefaultValue(p.DefaultValue, t) : null,
                                    p.IsDefined(typeof(ParamArrayAttribute), true));
            });
            var genericParameters =
                method.IsGenericMethodDefinition ? method.GetGenericArguments() :
                method.IsGenericMethod           ? ((R.MethodInfo)method).GetGenericMethodDefinition().GetGenericArguments() :
                                                   Type.EmptyTypes;
            var returnType =
                method is R.MethodInfo mi ? TypeReference.FromType(mi.ReturnType) :
                                            TypeSignature.Void;
            var isDestructor = method.Name == "Finalize" && !method.IsStatic && method is R.MethodInfo min && min.ReturnType == typeof(void) && min.GetParameters().Length == 0;
            return new MethodSignature(declaringType,
                                       parameters,
                                       method.Name,
                                       returnType,
                                       method.IsStatic,
                                       accessibility,
                                       method.IsVirtual && !method.IsFinal && declaringType.CanOverride,
                                       isOverride: (method as R.MethodInfo)?.GetBaseDefinition() != method,
                                       method.IsAbstract,
                                       method.IsSpecialName || isDestructor,
                                       genericParameters.EagerSelect(GenericParameter.FromType)
            );
        }

        static object CanonicalizeDefaultValue(object obj, TypeReference type)
        {
            if (obj == null) return obj;
            if (obj is string && type == TypeSignature.String) return obj;
            if (obj.GetType().IsPrimitive) return obj;
            if (obj.GetType().IsEnum)
            {
                return Convert.ChangeType(obj, Enum.GetUnderlyingType(obj.GetType()));
            }
            throw new NotSupportedException($"{obj} {obj.GetType()} {type}");
        }

    }
}
