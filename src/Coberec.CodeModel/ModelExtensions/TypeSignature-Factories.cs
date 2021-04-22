using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Coberec.CoreLib;
using Coberec.Utils;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Basic metadata about a type - <see cref="Name" />, <see cref="Kind" />, <see cref="Accessibility" />, ... </summary>
    public partial class TypeSignature
    {
        /// <summary> Creates a new signature of a `class`. </summary>
        public static TypeSignature Class(string name, TypeOrNamespace parent, Accessibility accessibility, bool canOverride = true, bool isAbstract = false, params GenericParameter[] genericParameters) =>
            new TypeSignature(name, parent, "class",
                accessibility: accessibility,
                canOverride: canOverride,
                isAbstract: isAbstract,
                isValueType: false,
                typeParameters: genericParameters.ToImmutableArray()
            );

        /// <summary> Creates a new signature of a `static class`. </summary>
        public static TypeSignature StaticClass(string name, TypeOrNamespace parent, Accessibility accessibility, params GenericParameter[] genericParameters) =>
            Class(name, parent, accessibility, canOverride: false, isAbstract: true, genericParameters);

        /// <summary> Creates a new signature of a `sealed class` (i.e. class that can not be extended). </summary>
        public static TypeSignature SealedClass(string name, TypeOrNamespace parent, Accessibility accessibility, params GenericParameter[] genericParameters) =>
            Class(name, parent, accessibility, canOverride: false, isAbstract: false, genericParameters);

        /// <summary> Creates a new signature of a `struct` (i.e. a value type) </summary>
        public static TypeSignature Struct(string name, TypeOrNamespace parent, Accessibility accessibility, params GenericParameter[] genericParameters) =>
            new TypeSignature(name, parent, "struct",
                accessibility: accessibility,
                canOverride: false,
                isAbstract: false,
                isValueType: true,
                typeParameters: genericParameters.ToImmutableArray()
            );


        /// <summary> Creates a new signature of a `interface`. </summary>
        public static TypeSignature Interface(string name, TypeOrNamespace parent, Accessibility accessibility, params GenericParameter[] genericParameters) =>
            new TypeSignature(name, parent, "interface",
                accessibility: accessibility,
                canOverride: true,
                isAbstract: true,
                isValueType: false,
                typeParameters: genericParameters.ToImmutableArray()
            );

        /// <summary> Gets a <see cref="TypeSignature"/> of the specified reflection <se cref="System.Type" />. If the type is generic, it must be the definition without the generic parameters instantiated. All the important metadata is copied from the reflection type, it can be used on any type even though it may not be valid in the specific <see cref="MetadataContext" />. </summary>
        public static TypeSignature FromType(Type type)
        {
            // Assert.True(!type.IsGenericType || type.IsGenericTypeDefinition);
            if (type.IsArray) throw new ArgumentException($"Can not create TypeSignature from array ({type})", nameof(type));
            if (type.IsByRef) throw new ArgumentException($"Can not create TypeSignature from reference ({type})", nameof(type));
            if (type.IsPointer) throw new ArgumentException($"Can not create TypeSignature from pointer ({type})", nameof(type));

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                type = type.GetGenericTypeDefinition();

            var parent = type.DeclaringType is object ? FromType(type.DeclaringType) :
                         type.Namespace is null       ? (TypeOrNamespace)NamespaceSignature.Global :
                                                        (TypeOrNamespace)NamespaceSignature.Parse(type.Namespace);
            var parentGenericArgs = type.DeclaringType?.GetGenericArguments().Length ?? 0;
            var kind =
                       type.IsEnum ? "enum" :
                       type.IsValueType ? "struct" :
                       type.IsInterface ? "interface" :
                       typeof(MulticastDelegate) == type.BaseType ? "delegate" :
                       type.IsClass ? "class" :
                       throw new NotSupportedException($"Can not translate {type} to TypeSignature");
            var accessibility = type.IsPublic || type.IsNestedPublic ? Accessibility.APublic :
                                type.IsNestedAssembly ? Accessibility.AInternal :
                                type.IsNestedPrivate ? Accessibility.APrivate :
                                type.IsNestedFamily ? Accessibility.AProtected :
                                type.IsNestedFamORAssem ? Accessibility.AProtectedInternal :
                                type.IsNestedFamANDAssem ? Accessibility.APrivateProtected :
                                                           Accessibility.AInternal;
            var typeName = type.Name.Contains('`') ? type.Name.Substring(0, type.Name.IndexOf("`", StringComparison.Ordinal)) :
                                                     type.Name;
            return new TypeSignature(typeName, parent, kind, type.IsValueType, !type.IsSealed, type.IsAbstract, accessibility, type.GetGenericArguments().Skip(parentGenericArgs).Select(GenericParameter.FromType).ToImmutableArray());
        }
    }
}
