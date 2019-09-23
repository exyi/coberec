using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Coberec.ExprCS
{
    public partial class TypeSignature
    {
        public static TypeSignature Void = Struct("Void", NamespaceSignature.System, Accessibility.APublic);
        public static TypeSignature Int32 = Struct("Int32", NamespaceSignature.System, Accessibility.APublic);
        public static TypeSignature TimeSpan = Struct("TimeSpan", NamespaceSignature.System, Accessibility.APublic);
        public static TypeSignature Object = Class("Object", NamespaceSignature.System, Accessibility.APublic);
        public static TypeSignature Boolean = Struct("Boolean", NamespaceSignature.System, Accessibility.APublic);
        public static TypeSignature String = SealedClass("String", NamespaceSignature.System, Accessibility.APublic);

        public static TypeSignature Class(string name, TypeOrNamespace parent, Accessibility accessibility, bool canOverride = true, bool isAbstract = false, int genericParamCount = 0) =>
            new TypeSignature(name, parent, "class",
                accessibility: accessibility,
                canOverride: canOverride,
                isAbstract: isAbstract,
                isValueType: false,
                genericParamCount: genericParamCount
            );

        public static TypeSignature StaticClass(string name, TypeOrNamespace parent, Accessibility accessibility, int genericParamCount = 0) =>
            Class(name, parent, accessibility, canOverride: false, isAbstract: true, genericParamCount);

        public static TypeSignature SealedClass(string name, TypeOrNamespace parent, Accessibility accessibility, int genericParamCount = 0) =>
            Class(name, parent, accessibility, canOverride: false, isAbstract: false, genericParamCount);

        public static TypeSignature Struct(string name, TypeOrNamespace parent, Accessibility accessibility, int genericParamCount = 0) =>
            new TypeSignature(name, parent, "struct",
                accessibility: accessibility,
                canOverride: false,
                isAbstract: false,
                isValueType: true,
                genericParamCount: genericParamCount
            );


        public static TypeSignature Interface(string name, TypeOrNamespace parent, Accessibility accessibility, int genericParamCount = 0) =>
            new TypeSignature(name, parent, "interface",
                accessibility: accessibility,
                canOverride: true,
                isAbstract: true,
                isValueType: false,
                genericParamCount: genericParamCount
            );

        public static TypeSignature FromType(Type type)
        {
            // TODO: asserts it's a definition not reference

            var parent = type.DeclaringType is object ? FromType(type.DeclaringType) : (TypeOrNamespace)NamespaceSignature.Parse(type.Namespace);
            var kind =
                       typeof(Delegate).IsAssignableFrom(type) ? "delegate" :
                       type.IsEnum ? "enum" :
                       type.IsValueType ? "struct" :
                       type.IsInterface ? "interface" :
                       type.IsClass ? "class" :
                       throw new NotSupportedException($"Can not translate {type} to TypeSignature");
            var accessibility = type.IsPublic || type.IsNestedPublic ? Accessibility.APublic :
                                type.IsNestedAssembly ? Accessibility.AInternal :
                                type.IsNestedPrivate ? Accessibility.APrivate :
                                type.IsNestedFamily ? Accessibility.AProtected :
                                type.IsNestedFamORAssem ? Accessibility.AProtectedInternal :
                                type.IsNestedFamANDAssem ? Accessibility.APrivateProtected :
                                throw new NotSupportedException("Unsupported accesibility of "+ type);
            return new TypeSignature(type.Name, parent, kind, type.IsValueType, !type.IsSealed, type.IsAbstract, accessibility, type.GetGenericArguments().Length);
        }

    }
}
