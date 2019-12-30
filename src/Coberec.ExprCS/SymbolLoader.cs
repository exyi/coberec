using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Coberec.CSharpGen;
using ICSharpCode.Decompiler.TypeSystem;
using TS=ICSharpCode.Decompiler.TypeSystem;

namespace Coberec.ExprCS
{
    /// <summary> Converts ILSpy typesystem member to ExprCS metadata. </summary>
    public static class SymbolLoader
    {
        internal static void RegisterDeclaredEntity(IEntity entity, MemberSignature member)
        {
            switch (member) {
                case FieldSignature field:
                    fieldSignatureCache.Add((IField)entity, field);
                    break;
                case MethodSignature method:
                    methodSignatureCache.Add((IMethod)entity, method);
                    break;
                case PropertySignature property:
                    propSignatureCache.Add((IProperty)entity, property);
                    break;
                case TypeSignature type:
                    typeSignatureCache.Add((ITypeDefinition)entity, type);
                    break;
                default:
                    throw new NotSupportedException($"{member.GetType()} {entity.GetType()}");
            }
        }

        static readonly ConditionalWeakTable<ITypeDefinition, TypeSignature> typeSignatureCache = new ConditionalWeakTable<ITypeDefinition, TypeSignature>();
        public static TypeSignature Type(IType type) =>
            typeSignatureCache.GetValue(type.GetDefinition(), type => {
                var parent = type.DeclaringTypeDefinition != null ?
                             TypeOrNamespace.TypeSignature(Type(type.DeclaringTypeDefinition)) :
                             TypeOrNamespace.NamespaceSignature(Namespace(type.Namespace));
                var kind = type.Kind == TypeKind.Interface ? "interface" :
                           type.Kind == TypeKind.Struct ? "struct" :
                           type.Kind == TypeKind.Class ? "class" :
                           type.Kind == TypeKind.Void ? "struct" :
                           type.Kind == TypeKind.Enum ? "enum" :
                           type.Kind == TypeKind.Delegate ? "delegate" :
                           throw new NotSupportedException($"Type kind {type.Kind} is not supported.");
                return new TypeSignature(type.Name, parent, kind, isValueType: !(bool)type.IsReferenceType, canOverride: !type.IsSealed && !type.IsStatic, isAbstract: type.IsAbstract || type.IsStatic, TranslateAccessibility(type.Accessibility), type.TypeParameters.Select(GenericParameter).ToImmutableArray());
            });

        static readonly ConditionalWeakTable<IMethod, MethodSignature> methodSignatureCache = new ConditionalWeakTable<IMethod, MethodSignature>();
        public static MethodSignature Method(IMethod m) =>
            methodSignatureCache.GetValue((IMethod)m.MemberDefinition, m =>
                new MethodSignature(
                    Type(m.DeclaringType),
                    m.Parameters.Select(Parameter).ToImmutableArray(),
                    m.Name,
                    TypeRef(m.ReturnType),
                    m.IsStatic,
                    TranslateAccessibility(m.Accessibility),
                    m.IsVirtual,
                    m.IsOverride,
                    m.IsAbstract,
                    m.IsConstructor || m.IsAccessor || m.IsOperator || m.IsDestructor,
                    m.TypeParameters.Select(GenericParameter).ToImmutableArray()
                )
            );

        static readonly ConditionalWeakTable<IField, FieldSignature> fieldSignatureCache = new ConditionalWeakTable<IField, FieldSignature>();
        public static FieldSignature Field(IField field) =>
            fieldSignatureCache.GetValue(field, f =>
                new FieldSignature(
                    Type(f.DeclaringType),
                    f.Name,
                    TranslateAccessibility(field.Accessibility),
                    TypeRef(f.ReturnType),
                    f.IsStatic,
                    f.IsReadOnly
                )
            );

        static readonly ConditionalWeakTable<IProperty, PropertySignature> propSignatureCache = new ConditionalWeakTable<IProperty, PropertySignature>();
        public static PropertySignature Property(IProperty property) =>
            propSignatureCache.GetValue(property, p =>
                new PropertySignature(
                    Type(p.DeclaringType),
                    TypeRef(p.ReturnType),
                    p.Name,
                    TranslateAccessibility(p.Accessibility),
                    p.IsStatic,
                    p.Getter?.Apply(Method),
                    p.Setter?.Apply(Method)
                )
            );

        static readonly ConditionalWeakTable<ITypeParameter, GenericParameter> typeParameterCache = new ConditionalWeakTable<ITypeParameter, GenericParameter>();
        public static GenericParameter GenericParameter(ITypeParameter parameter) =>
            typeParameterCache.GetValue(parameter, p => ExprCS.GenericParameter.Get(parameter.Owner, parameter.Name));

        internal static void RegisterTypeParameter(ITypeParameter p1, GenericParameter p2) =>
            typeParameterCache.Add(p1, p2);

        public static Accessibility TranslateAccessibility(TS.Accessibility a) =>
            a == TS.Accessibility.Internal ? Accessibility.AInternal :
            a == TS.Accessibility.Private ? Accessibility.APrivate :
            a == TS.Accessibility.Public ? Accessibility.APublic :
            a == TS.Accessibility.Protected ? Accessibility.AProtected :
            a == TS.Accessibility.ProtectedOrInternal ? Accessibility.AProtectedInternal :
            a == TS.Accessibility.ProtectedAndInternal ? Accessibility.APrivateProtected :
            throw new NotSupportedException($"{a}");

        static readonly ConditionalWeakTable<string, NamespaceSignature> namespaceSignatureCache = new ConditionalWeakTable<string, NamespaceSignature>();
        public static NamespaceSignature Namespace(string ns) =>
            namespaceSignatureCache.GetValue(ns, NamespaceSignature.Parse);

        public static MemberSignature Member(IMember m) =>
            m is ITypeDefinition type ? Type(type) :
            m is IMethod method       ? (MemberSignature)Method(method) :
            throw new NotSupportedException($"Member '{m}' of type '{m.GetType().Name}' is not supported");

        public static MethodParameter Parameter(IParameter parameter) =>
            new MethodParameter(
                TypeRef(parameter.Type),
                parameter.Name,
                parameter.HasConstantValueInSignature,
                parameter.HasConstantValueInSignature ? parameter.GetConstantValue() : null,
                parameter.IsParams
            );

        public static TypeReference TypeRef(IType type) =>
            type is TS.Implementation.NullabilityAnnotatedType decoratedType ? TypeRef(decoratedType.TypeWithoutAnnotation) :
            type is ITypeDefinition td ? TypeReference.SpecializedType(Type(td), ImmutableArray<TypeReference>.Empty) :
            type is TS.ByReferenceType refType ? TypeReference.ByReferenceType(TypeRef(refType.ElementType)) :
            type is TS.PointerType ptrType ? TypeReference.PointerType(TypeRef(ptrType.ElementType)) :
            type is TS.ArrayType arrType ? TypeReference.ArrayType(TypeRef(arrType.ElementType), arrType.Dimensions) :
            type is TS.ParameterizedType paramType ? TypeReference.SpecializedType(
                                                         Type(paramType.GenericType.GetDefinition()),
                                                         paramType.TypeArguments.Select(TypeRef).ToImmutableArray()) :
            type is TS.ITypeParameter typeParam ? TypeReference.GenericParameter(GenericParameter(typeParam)) :
            throw new NotImplementedException($"Type reference '{type}' of type '{type.GetType().Name}' is not supported.");

    }
}
