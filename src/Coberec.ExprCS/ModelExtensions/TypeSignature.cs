using System;
using System.Collections.Generic;
using System.Linq;

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


    }
}
