using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Coberec.Utils;
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
            type is null ? throw new ArgumentNullException(nameof(type)) :
            type.Kind == TypeKind.Unknown ? UnkownType(type) :
            !(type.GetDefinition() is ITypeDefinition typeDef) ? throw new ArgumentException($"Type '{type}' does not have a definition, so type signature can not be loaded. Did you intent to use SymbolLoader.TypeRef?", nameof(type)) :
            typeDef.KnownTypeCode switch {
                KnownTypeCode.Void => TypeSignature.Void,
                KnownTypeCode.Object => TypeSignature.Object,
                KnownTypeCode.Boolean => TypeSignature.Boolean,
                KnownTypeCode.Double => TypeSignature.Double,
                KnownTypeCode.Single => TypeSignature.Single,
                KnownTypeCode.Byte => TypeSignature.Byte,
                KnownTypeCode.UInt16 => TypeSignature.UInt16,
                KnownTypeCode.UInt32 => TypeSignature.UInt32,
                KnownTypeCode.UInt64 => TypeSignature.UInt64,
                KnownTypeCode.Int16 => TypeSignature.Int16,
                KnownTypeCode.Int32 => TypeSignature.Int32,
                KnownTypeCode.Int64 => TypeSignature.Int64,
                KnownTypeCode.String => TypeSignature.String,
                KnownTypeCode.IEnumerableOfT => TypeSignature.IEnumerableOfT,
                KnownTypeCode.IEnumeratorOfT => TypeSignature.IEnumeratorOfT,
                KnownTypeCode.NullableOfT => TypeSignature.NullableOfT,
                _ => null
            } ??
            typeSignatureCache.GetValue(typeDef, type => {
                var parent = type.DeclaringTypeDefinition != null ?
                             TypeOrNamespace.TypeSignature(Type(type.DeclaringTypeDefinition)) :
                             TypeOrNamespace.NamespaceSignature(Namespace(type.Namespace));
                var parentGenerics = type.DeclaringTypeDefinition?.TypeParameterCount ?? 0;
                var kind = type.Kind == TypeKind.Interface ? "interface" :
                           type.Kind == TypeKind.Struct ? "struct" :
                           type.Kind == TypeKind.Class ? "class" :
                           type.Kind == TypeKind.Void ? "struct" :
                           type.Kind == TypeKind.Enum ? "enum" :
                           type.Kind == TypeKind.Delegate ? "delegate" :
                           throw new NotSupportedException($"Type kind {type.Kind} is not supported.");


                return new TypeSignature(type.Name, parent, kind, isValueType: type.IsReferenceType == false, canOverride: !type.IsSealed && !type.IsStatic, isAbstract: type.IsAbstract || type.IsStatic, TranslateAccessibility(type.Accessibility), type.TypeParameters.Skip(parentGenerics).Select(GenericParameter).ToImmutableArray());
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
                    (m.IsVirtual || m.IsAbstract || m.IsOverride) && !m.IsSealed && !m.DeclaringTypeDefinition.IsSealed && m.DeclaringTypeDefinition.IsReferenceType == true,
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
            ns.Length == 0 ? NamespaceSignature.Global :
            namespaceSignatureCache.GetValue(ns, NamespaceSignature.Parse);

        public static MemberSignature Entity(IEntity m, bool throwIfNotSupported = true) =>
            m is ITypeDefinition type ? Type(type) :
            m is IMethod method       ? (MemberSignature)Method(method) :
            m is IProperty property   ? (PropertySignature)Property(property) :
            m is IField field         ? Field(field) :
            throwIfNotSupported ? throw new NotSupportedException($"Entity '{m}' of type '{m.GetType().Name}' is not supported") :
            null;

        public static MethodParameter Parameter(IParameter parameter) =>
            new MethodParameter(
                TypeRef(parameter.Type),
                parameter.Name,
                parameter.HasConstantValueInSignature,
                parameter.HasConstantValueInSignature ? parameter.GetConstantValue() : null,
                parameter.IsParams
            );
        public const bool AllowUnknwonTypes = true;
        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }
        private static readonly Lazy<Dictionary<string, Type>> typeLookup = new Lazy<Dictionary<string, Type>>(() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(t => t.GetLoadableTypes())
                .GroupBy(t => t.FullName)
                .ToDictionary(t => t.Key, t => t.First())
        );

        public static TypeSignature UnkownType(IType type)
        {
            if (AllowUnknwonTypes)
            {
                var name = type.ReflectionName;
                var reflectionType = System.Type.GetType(name) ?? typeLookup.Value.GetValueOrDefault(name);
                if (reflectionType != null)
                    return TypeSignature.FromType(reflectionType);
            }

            throw new ArgumentException($"Can not convert unknown type {type}. Did you forget to include it's assembly in the set of references?", "type");
        }
        public static TypeReference UnkownTypeRef(IType type)
        {
            if (AllowUnknwonTypes)
            {
                var name = type.ReflectionName;
                var reflectionType = System.Type.GetType(name) ?? typeLookup.Value.GetValueOrDefault(name);
                if (reflectionType != null)
                    return TypeReference.FromType(reflectionType);
            }

            throw new ArgumentException($"Can not convert unknown type {type}. Did you forget to include it's assembly in the set of references?", "type");
        }



        public static TypeReference TypeRef(IType type) =>
            type is null ? throw new ArgumentNullException("type") :
            type is TS.Implementation.NullabilityAnnotatedType decoratedType ? TypeRef(decoratedType.TypeWithoutAnnotation) :
            type is ITypeDefinition td ? Type(td).SpecializeByItself() :
            type is TS.ByReferenceType refType ? TypeReference.ByReferenceType(TypeRef(refType.ElementType)) :
            type is TS.PointerType ptrType ? TypeReference.PointerType(TypeRef(ptrType.ElementType)) :
            type is TS.ArrayType arrType ? TypeReference.ArrayType(TypeRef(arrType.ElementType), arrType.Dimensions) :
            type is TS.ParameterizedType paramType ? TypeReference.SpecializedType(
                                                         Type(paramType.GenericType),
                                                         paramType.TypeArguments.Select(TypeRef).ToImmutableArray()) :
            type is TS.TupleType tupleType ? TypeReference.Tuple(tupleType.ElementTypes.EagerSelect(TypeRef)) :
            type is TS.ITypeParameter typeParam ? TypeReference.GenericParameter(GenericParameter(typeParam)) :
            type.Kind == TypeKind.Unknown ? UnkownTypeRef(type) :
            throw new NotImplementedException($"Type reference '{type}' of type '{type.GetType().Name}' is not supported.");

        public static MethodReference MethodRef(IMethod m)
        {
            var t = ((TypeReference.SpecializedTypeCase)TypeRef(m.DeclaringType)).Item;
            var signature = Method(m);
            var args = m.TypeArguments.Select(TypeRef).ToImmutableArray();
            return new MethodReference(signature, t.TypeArguments, args);
        }
    }
}
