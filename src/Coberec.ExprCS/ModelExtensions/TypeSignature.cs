using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Coberec.ExprCS
{
    public partial class TypeSignature
    {
        /// <summary> Signature of <see cref="System.Void" /> </summary>
        public static TypeSignature Void = Struct("Void", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Int32" /> </summary>
        public static TypeSignature Int32 = Struct("Int32", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.TimeSpan" /> </summary>
        public static TypeSignature TimeSpan = Struct("TimeSpan", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Object" /> </summary>
        public static TypeSignature Object = Class("Object", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Boolean" /> </summary>
        public static TypeSignature Boolean = Struct("Boolean", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.String" /> </summary>
        public static TypeSignature String = SealedClass("String", NamespaceSignature.System, Accessibility.APublic);

        /// <summary> Creates a new signature of a `class`. </summary>
        public static TypeSignature Class(string name, TypeOrNamespace parent, Accessibility accessibility, bool canOverride = true, bool isAbstract = false, int genericParamCount = 0) =>
            new TypeSignature(name, parent, "class",
                accessibility: accessibility,
                canOverride: canOverride,
                isAbstract: isAbstract,
                isValueType: false,
                genericParamCount: genericParamCount
            );

        /// <summary> Creates a new signature of a `static class`. </summary>
        public static TypeSignature StaticClass(string name, TypeOrNamespace parent, Accessibility accessibility, int genericParamCount = 0) =>
            Class(name, parent, accessibility, canOverride: false, isAbstract: true, genericParamCount);

        /// <summary> Creates a new signature of a `sealed class` (i.e. class that can not be extended). </summary>
        public static TypeSignature SealedClass(string name, TypeOrNamespace parent, Accessibility accessibility, int genericParamCount = 0) =>
            Class(name, parent, accessibility, canOverride: false, isAbstract: false, genericParamCount);

        /// <summary> Creates a new signature of a `struct` (i.e. a value type) </summary>
        public static TypeSignature Struct(string name, TypeOrNamespace parent, Accessibility accessibility, int genericParamCount = 0) =>
            new TypeSignature(name, parent, "struct",
                accessibility: accessibility,
                canOverride: false,
                isAbstract: false,
                isValueType: true,
                genericParamCount: genericParamCount
            );


        /// <summary> Creates a new signature of a `interface`. </summary>
        public static TypeSignature Interface(string name, TypeOrNamespace parent, Accessibility accessibility, int genericParamCount = 0) =>
            new TypeSignature(name, parent, "interface",
                accessibility: accessibility,
                canOverride: true,
                isAbstract: true,
                isValueType: false,
                genericParamCount: genericParamCount
            );

        /// <summary> Gets a <see cref="TypeSignature"/> of the specified reflection <se cref="System.Type" />. If the type is generic, it must be the definition without the generic parameters instantiated. All the important metadata is copied from the reflection type, it can be used on any type even though it may not be valid in the specific <see cref="MetadataContext" />. </summary>
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
            var typeName = type.Name.Contains('`') ? type.Name.Substring(0, type.Name.IndexOf("`")) :
                                                     type.Name;
            return new TypeSignature(typeName, parent, kind, type.IsValueType, !type.IsSealed, type.IsAbstract, accessibility, type.GetGenericArguments().Length);
        }


        public override string ToString()
        {
            if (this == Void) return "void";
            else if (this == Int32) return "int";
            else if (this == Object) return "object";
            else if (this == String) return "string";

            var sb = new System.Text.StringBuilder();
            if (this.Accessibility != Accessibility.APublic) sb.Append(this.Accessibility).Append(" ");
            if (this.IsAbstract) sb.Append("abstract ");
            if (this.Kind == "class" && !this.CanOverride) sb.Append("sealed ");
            if (this.Kind != "class") sb.Append(this.Kind).Append(" ");
            sb.Append(this.GetFullTypeName());
            return sb.ToString();
        }
    }
}
