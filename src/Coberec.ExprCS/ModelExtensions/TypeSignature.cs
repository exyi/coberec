using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Coberec.CoreLib;
using Coberec.CSharpGen;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Basic metadata about a type - <see cref="Name" />, <see cref="Kind" />, <see cref="Accessibility" />, ... </summary>
    public partial class TypeSignature
    {

        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, TypeSignature t)
        {
            if (t.IsValueType)
            {
                if (t.CanOverride) e.Add(ValidationErrors.Create($"Can not override value type {t}").Nest("canOverride"));
                if (t.IsAbstract) e.Add(ValidationErrors.Create($"Can not have abstract value type {t}").Nest("isAbstract"));
            }
        }
        /// <summary> Returns total type parameter count (including those from parent types) </summary>
        public int TotalParameterCount() => this.Parent.Match(ns => 0, t => t.TotalParameterCount()) + this.TypeParameters.Length;
        /// <summary> Returns all type parameters (including those from parent types) </summary>
        public ImmutableArray<GenericParameter> AllTypeParameters() =>
            this.Parent.Match(ns => ImmutableArray<GenericParameter>.Empty, t => t.AllTypeParameters())
            .AddRange(this.TypeParameters);

        public string ReflectionName() =>
            this.Parent.Match(ns => ns.ToString() + ".", t => t.ReflectionName() + "+")
            + this.Name
            + (this.TypeParameters.Length > 0 ? "`" + this.TypeParameters.Length : "");

        /// <summary> Returns a specialized with the generic parameter form itself filled in. You probably don't want to use that to create expression, but may be quite useful to get base types with generic parameters from this type. </summary>
        public SpecializedType SpecializeByItself() => new SpecializedType(this, this.AllTypeParameters().EagerSelect(TypeReference.GenericParameter));

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
            if (argsA.Length != TotalParameterCount())
                throw new ArgumentException($"Can not specialize type '{this}' by [{string.Join(", ", argsA)}]. Expected {TotalParameterCount()} type parameters.", nameof(args));
            return new SpecializedType(this, argsA);
        }

        public bool IsPrimitive()
        {
            if (!this.IsValueType || this.Parent != NamespaceSignature.System) return false;

            return this == Int32 ||
                   this == Boolean ||
                   this == Int64 ||
                   this == Int16 ||
                   this == UInt32 ||
                   this == UInt64 ||
                   this == UInt16 ||
                   this == Double ||
                   this == Single ||
                   this == Byte ||
                   this == SByte ||
                   this == UIntPtr ||
                   this == IntPtr;
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
