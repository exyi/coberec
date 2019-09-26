using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Coberec.CSharpGen;
using ICSharpCode.Decompiler.TypeSystem;
using TS=ICSharpCode.Decompiler.TypeSystem;

namespace Coberec.ExprCS
{
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
        public static TypeSignature Type(ITypeDefinition t) =>
            typeSignatureCache.GetValue(t, type => {
                var parent = type.DeclaringTypeDefinition != null ?
                             TypeOrNamespace.TypeSignature(Type(type.DeclaringTypeDefinition)) :
                             TypeOrNamespace.NamespaceSignature(Namespace(type.Namespace));
                var kind = t.Kind == TypeKind.Interface ? "interface" :
                           t.Kind == TypeKind.Struct ? "struct" :
                           t.Kind == TypeKind.Class ? "class" :
                           t.Kind == TypeKind.Void ? "struct" :
                           t.Kind == TypeKind.Enum ? "enum" :
                           t.Kind == TypeKind.Delegate ? "delegate" :
                           throw new NotSupportedException($"Type kind {t.Kind} is not supported.");
                return new TypeSignature(type.Name, parent, kind, isValueType: !(bool)type.IsReferenceType, canOverride: !type.IsSealed && !type.IsStatic, isAbstract: type.IsAbstract || type.IsStatic, TranslateAccessibility(type.Accessibility), type.TypeParameterCount);
            });

        static readonly ConditionalWeakTable<IMethod, MethodSignature> methodSignatureCache = new ConditionalWeakTable<IMethod, MethodSignature>();
        public static MethodSignature Method(IMethod method) =>
            methodSignatureCache.GetValue(method, m =>
                new MethodSignature(
                    Type(m.DeclaringType.GetDefinition()),
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
                    Type(f.DeclaringType.GetDefinition()),
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
                    Type(p.DeclaringType.GetDefinition()),
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
            typeParameterCache.GetValue(parameter, p => new GenericParameter(Guid.NewGuid(), p.Name));

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
                parameter.Name
            );

        public static TypeReference TypeRef(IType type) =>
            type is ITypeDefinition td ? TypeReference.SpecializedType(Type(td), ImmutableArray<TypeReference>.Empty) :
            type is TS.ByReferenceType refType ? TypeReference.ByReferenceType(TypeRef(refType.ElementType)) :
            type is TS.PointerType ptrType ? TypeReference.PointerType(TypeRef(ptrType.ElementType)) :
            type is TS.ArrayType arrType ? TypeReference.ArrayType(TypeRef(arrType.ElementType), arrType.Dimensions) :
            type is TS.ParameterizedType paramType ? TypeReference.SpecializedType(
                                                         Type(paramType.GenericType.GetDefinition()),
                                                         paramType.TypeArguments.Select(TypeRef).ToImmutableArray()) :
            type is TS.ITypeParameter typeParam ? TypeReference.GenericParameter(GenericParameter(typeParam)) :
            type is TS.Implementation.NullabilityAnnotatedType decoratedType ? TypeRef(decoratedType.TypeWithoutAnnotation) :
            throw new NotImplementedException($"Type reference '{type}' of type '{type.GetType().Name}' is not supported.");

    }
}
