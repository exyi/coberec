using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Coberec.CSharpGen;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Basic metadata about a type - <see cref="Name" />, <see cref="Kind" />, <see cref="Accessibility" />, ... </summary>
    public partial class TypeSignature
    {
        /// <summary> Signature of <see cref="System.Void" /> </summary>
        public static readonly TypeSignature Void = Struct("Void", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Int32" /> </summary>
        public static readonly TypeSignature Int32 = Struct("Int32", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Double" /> </summary>
        public static readonly TypeSignature Double = Struct("Double", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.TimeSpan" /> </summary>
        public static readonly TypeSignature TimeSpan = Struct("TimeSpan", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Object" /> </summary>
        public static readonly TypeSignature Object = Class("Object", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Boolean" /> </summary>
        public static readonly TypeSignature Boolean = Struct("Boolean", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.String" /> </summary>
        public static readonly TypeSignature String = SealedClass("String", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Collections.IEnumerable" /> </summary>
        public static readonly TypeSignature IEnumerable = FromType(typeof(System.Collections.IEnumerable));
        /// <summary> Signature of <see cref="System.Collections.IEnumerable" /> </summary>
        public static readonly TypeSignature IEnumerator = FromType(typeof(System.Collections.IEnumerator));
        /// <summary> Signature of <see cref="System.Collections.Generic.IEnumerable{T}" /> </summary>
        public static readonly TypeSignature IEnumerableOfT = FromType(typeof(IEnumerable<>));
        /// <summary> Signature of <see cref="System.Collections.Generic.IEnumerator{T}" /> </summary>
        public static readonly TypeSignature IEnumeratorOfT = FromType(typeof(IEnumerable<>));
        /// <summary> Signature of <see cref="System.Nullable{T}" /> </summary>
        public static readonly TypeSignature NullableOfT = FromType(typeof(Nullable<>));

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
            var typeName = type.Name.Contains('`') ? type.Name.Substring(0, type.Name.IndexOf("`", StringComparison.Ordinal)) :
                                                     type.Name;
            return new TypeSignature(typeName, parent, kind, type.IsValueType, !type.IsSealed, type.IsAbstract, accessibility, type.GetGenericArguments().Select(GenericParameter.FromType).ToImmutableArray());
        }

        /// <summary> Returns a specialized with the generic parameter form itself filled in. You probably don't want to use that to create expression, but may be quite useful to get base types with generic parameters from this type. </summary>
        public SpecializedType SpecializeByItself() => new SpecializedType(this, this.TypeParameters.EagerSelect(TypeReference.GenericParameter));

        /// <summary> Asserts that the type signature is not generic and then makes a <see cref="SpecializedType" /> from itself. </summary>
        public SpecializedType NotGeneric()
        {
            Assert.Empty(this.TypeParameters);
            return new SpecializedType(this, ImmutableArray<TypeReference>.Empty);
        }

        /// <summary> Returns a specialized with the generic parameter form itself filled in. You probably don't want to use that to create expression, but may be quite useful to get base types with generic parameters from this type. </summary>
        public SpecializedType Specialize(params TypeReference[] args) =>
            Specialize(args.AsEnumerable());
        /// <summary> Returns a specialized with the generic parameter form itself filled in. You probably don't want to use that to create expression, but may be quite useful to get base types with generic parameters from this type. </summary>
        public SpecializedType Specialize(IEnumerable<TypeReference> args)
        {
            var argsA = args.ToImmutableArray();
            Assert.Equal(argsA.Length, this.TypeParameters.Length); // TODO: does it work for nested types?
            return new SpecializedType(this, argsA);
        }


        public override string ToString()
        {
            if (this == Void) return "void";
            else if (this == Int32) return "int";
            else if (this == Object) return "object";
            else if (this == String) return "string";

            var sb = new System.Text.StringBuilder();
            if (this.Accessibility != Accessibility.APublic) sb.Append(this.Accessibility).Append(" ");
            if (this.IsAbstract && !this.CanOverride) sb.Append("static ");
            else
            {
                if (this.Kind != "interface" && this.IsAbstract) sb.Append("abstract ");
                if (this.Kind == "class" && !this.CanOverride) sb.Append("sealed ");
            }
            if (this.Kind != "class") sb.Append(this.Kind).Append(" ");
            sb.Append(this.GetFullTypeName());
            return sb.ToString();
        }
    }
}
