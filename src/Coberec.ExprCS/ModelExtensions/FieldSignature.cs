using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using Coberec.CSharpGen;
using Xunit;
using R = System.Reflection;

namespace Coberec.ExprCS
{
    /// <summary> Basic metadata about a field - <see cref="Name"/>, <see cref="Accessibility" />, <see cref="DeclaringType" />, ... </summary>
    public partial class FieldSignature
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, FieldSignature f)
        {
        }

        /// <summary> Fills in the generic parameters. </summary>
        public FieldReference Specialize(IEnumerable<TypeReference> typeArgs) =>
            new FieldReference(this, typeArgs.ToImmutableArray());

        /// <summary> Fills in the generic parameters from the declaring type. Useful when using the field inside it's declaring type. </summary>
        public FieldReference SpecializeFromDeclaringType() =>
            new FieldReference(this, this.DeclaringType.AllTypeParameters().EagerSelect(TypeReference.GenericParameter));

        public static implicit operator FieldReference(FieldSignature signature)
        {
            if (signature == null) return null;
            Assert.Empty(signature.DeclaringType.TypeParameters);
            return new FieldReference(signature, ImmutableArray<TypeReference>.Empty);
        }

        public override string ToString() =>
            ToString(this, this.ResultType);

        internal static string ToString(FieldSignature s, TypeReference resultType)
        {
            var sb = new System.Text.StringBuilder();
            if (s.Accessibility != Accessibility.APublic) sb.Append(s.Accessibility).Append(" ");
            if (s.IsStatic) sb.Append("static ");
            if (s.IsReadonly) sb.Append("readonly ");
            sb.Append(s.Name);
            sb.Append(": ");
            sb.Append(resultType);
            return sb.ToString();
        }

        public static FieldSignature FromReflection(R.FieldInfo field)
        {
            field = MethodSignature.SanitizeDeclaringTypeGenerics(field);

            var declaringType = TypeSignature.FromType(field.DeclaringType);
            var accessibility = field.IsPublic ? Accessibility.APublic :
                                field.IsAssembly ? Accessibility.AInternal :
                                field.IsPrivate ? Accessibility.APrivate :
                                field.IsFamily ? Accessibility.AProtected :
                                field.IsFamilyOrAssembly ? Accessibility.AProtectedInternal :
                                field.IsFamilyAndAssembly ? Accessibility.APrivateProtected :
                                throw new NotSupportedException("Unsupported accesibility of " + field);

            var resultType = TypeReference.FromType(field.FieldType);
            return new FieldSignature(declaringType,
                                      field.Name,
                                      accessibility,
                                      resultType,
                                      field.IsStatic,
                                      field.IsInitOnly);
        }
    }
}
