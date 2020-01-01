using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;
using Xunit;
using R = System.Reflection;

namespace Coberec.ExprCS
{
    /// <summary> Basic metadata about a method - <see cref="Name" />, <see cref="Accessibility" />, <see cref="Params" />, <see cref="DeclaringType" />, ... </summary>
    public partial class MethodSignature
    {
        /// <summary> Signature of method <see cref="Object.ToString" /> </summary>
        public static readonly MethodSignature Object_ToString = MethodReference.FromLambda<object>(a => a.ToString()).Signature;
        /// <summary> Signature of method <see cref="Object.GetType" /> </summary>
        public static readonly MethodSignature Object_GetType = MethodReference.FromLambda<object>(a => a.GetType()).Signature;
        /// <summary> Signature of method <see cref="Object.GetHashCode" /> </summary>
        public static readonly MethodSignature Object_GetHashCode = MethodReference.FromLambda<object>(a => a.GetHashCode()).Signature;
        /// <summary> Signature of method <see cref="Object.Equals(object)" /> </summary>
        public static readonly MethodSignature Object_Equals = MethodReference.FromLambda<object>(a => a.Equals(null)).Signature;
        /// <summary> Signature of method <see cref="Object.Object" /> </summary>
        public static readonly MethodSignature Object_Constructor = MethodReference.FromLambda(() => new object()).Signature;

        /// <summary> Creates new method signature that is a constructor </summary>
        public static MethodSignature Constructor(TypeSignature declaringType, Accessibility accessibility, params MethodParameter[] parameters) =>
            Constructor(declaringType, accessibility, parameters.AsEnumerable());
        /// <summary> Creates new method signature that is a constructor </summary>
        public static MethodSignature Constructor(TypeSignature declaringType, Accessibility accessibility, IEnumerable<MethodParameter> parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), ".ctor", TypeSignature.Void, isStatic: false, accessibility, isVirtual: false, isOverride: false, isAbstract: false, hasSpecialName: true, ImmutableArray<GenericParameter>.Empty);

        /// <summary> Creates new static method signature </summary>
        public static MethodSignature Static(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, params MethodParameter[] parameters) =>
            Static(name, declaringType, accessibility, returnType, parameters.AsEnumerable());
        /// <summary> Creates new static method signature </summary>
        public static MethodSignature Static(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, IEnumerable<MethodParameter> parameters, IEnumerable<GenericParameter> genericParameters = null) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: true, accessibility, isVirtual: false, isOverride: false, isAbstract: false, hasSpecialName: false, genericParameters?.ToImmutableArray() ?? ImmutableArray<GenericParameter>.Empty);

        /// <summary> Creates new instance method signature. The method is not override, not virtual, not abstract </summary>
        public static MethodSignature Instance(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, params MethodParameter[] parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: false, accessibility, isVirtual: false, isOverride: false, isAbstract: false, hasSpecialName: false, ImmutableArray<GenericParameter>.Empty);

        /// <summary> Creates new instance abstract method signature. It is not override, but you can apply `.With(isOverride: true)` to the result to make it. </summary>
        public static MethodSignature Abstract(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, params MethodParameter[] parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: false, accessibility, isVirtual: true, isOverride: false, isAbstract: true, hasSpecialName: false, ImmutableArray<GenericParameter>.Empty);

        /// <summary> Creates new method signature with brand new type parameters. Since you can't have declared multiple methods with the same type parameters, you should use Clone to create similar method signatures. </summary>
        public MethodSignature Clone()
        {
            if (this.TypeParameters.Length == 0) return this;
            var newTypeParams = TypeParameters.EagerSelect(tp => new GenericParameter(Guid.NewGuid(), tp.Name));
            var resultType = ResultType.SubstituteGenerics(TypeParameters, newTypeParams.EagerSelect(TypeReference.GenericParameter));
            var @params = Params.EagerSelect(p => p.SubstituteGenerics(TypeParameters, newTypeParams.EagerSelect(TypeReference.GenericParameter)));
            return this.With(@params: @params, resultType: resultType, typeParameters: newTypeParams);
        }

        public static MethodSignature Override(TypeSignature declaringType, MethodSignature overridenMethod, bool isVirtual = true, bool isAbstract = false)
        {
            overridenMethod = overridenMethod.Clone();
            return overridenMethod.With(declaringType, name: overridenMethod.Name, isVirtual: isVirtual, isOverride: true, isAbstract: isAbstract);
        }

        /// <summary> Fills in the generic parameters. </summary>
        public MethodReference Specialize(IEnumerable<TypeReference> typeArgs, IEnumerable<TypeReference> methodArgs) =>
            new MethodReference(this, typeArgs?.ToImmutableArray() ?? ImmutableArray<TypeReference>.Empty, methodArgs?.ToImmutableArray() ?? ImmutableArray<TypeReference>.Empty);

        /// <summary> Fills in the generic parameters from the declaring type. Useful when using the method inside it's declaring type. </summary>
        public MethodReference SpecializeFromDeclaringType(IEnumerable<TypeReference> methodArgs) =>
            new MethodReference(this, this.DeclaringType.TypeParameters.EagerSelect(TypeReference.GenericParameter), methodArgs?.ToImmutableArray() ?? ImmutableArray<TypeReference>.Empty);
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

        public override string ToString() =>
            ToString(this, this.TypeParameters, this.Params, this.ResultType);

        internal static string ToString(MethodSignature s, IEnumerable<object> typeArgs, ImmutableArray<MethodParameter> parameters, TypeReference resultType)
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
        public bool IsConstructor() => this.HasSpecialName && this.Name == ".ctor" && !this.IsStatic;
        /// <summary> Returns true if this method is a static constructor (for instance ones see <see cref="IsConstructor" /> </summary>
        public bool IsStaticConstructor() => this.HasSpecialName && this.Name == ".cctor" && this.IsStatic;

        public static MethodSignature FromReflection(R.MethodBase method)
        {
            var declaringType = TypeSignature.FromType(method.DeclaringType);
            var accessibility = method.IsPublic ? Accessibility.APublic :
                                method.IsAssembly ? Accessibility.AInternal :
                                method.IsPrivate ? Accessibility.APrivate :
                                method.IsFamily ? Accessibility.AProtected :
                                method.IsFamilyOrAssembly ? Accessibility.AProtectedInternal :
                                method.IsFamilyAndAssembly ? Accessibility.APrivateProtected :
                                throw new NotSupportedException("Unsupported accessibility of " + method);
            var parameters = method.GetParameters().EagerSelect(p =>
                new MethodParameter(TypeReference.FromType(p.ParameterType),
                                    p.Name,
                                    p.HasDefaultValue,
                                    p.HasDefaultValue ? p.DefaultValue : null,
                                    p.IsDefined(typeof(ParamArrayAttribute), true)
            ));
            var genericParameters =
                method.IsGenericMethodDefinition ? method.GetGenericArguments() :
                method.IsGenericMethod           ? ((R.MethodInfo)method).GetGenericMethodDefinition().GetGenericArguments() :
                                                   Type.EmptyTypes;
            var returnType =
                method is R.MethodInfo mi ? TypeReference.FromType(mi.ReturnType) :
                                            TypeSignature.Void;
            return new MethodSignature(declaringType,
                                       parameters,
                                       method.Name,
                                       returnType,
                                       method.IsStatic,
                                       accessibility,
                                       method.IsVirtual,
                                       isOverride: (method as R.MethodInfo)?.GetBaseDefinition() != method,
                                       method.IsAbstract,
                                       method.IsSpecialName,
                                       genericParameters.EagerSelect(GenericParameter.FromType)
            );
        }

    }
}
