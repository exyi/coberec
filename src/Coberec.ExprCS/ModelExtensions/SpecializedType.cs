using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using Coberec.CSharpGen;

namespace Coberec.ExprCS
{
    public partial class SpecializedType
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, SpecializedType t)
        {
            if (t.Type is null) return;
            var expectedCount = t.Type.TotalParameterCount();
            if (expectedCount != t.TypeArguments.Length)
                e.Add(ValidationErrors.Create($"Type {t.Type} expected {expectedCount} parameters, got [{string.Join(", ", t.TypeArguments)}]"));
        }

        public SpecializedType(TypeSignature type, params TypeReference[] genericArgs)
            : this(type, genericArgs.ToImmutableArray()) {}

        /// <summary> Gets a declaring type (parent type) if it exists. If the type is global (directly in a namespace), `null` is returned. </summary>
        public SpecializedType DeclaringType() =>
            this.Type.Parent.Match(
                ns => null,
                t => t.Specialize(this.TypeArguments.Take(t.TypeParameters.Length).ToImmutableArray())
            );

        public SpecializedType SubstituteGenerics(
            IEnumerable<GenericParameter> parameters,
            IEnumerable<TypeReference> arguments) =>
            this.TypeArguments.Length == 0 ? this :
            SubstituteGenerics(parameters.ToImmutableArray(), arguments.ToImmutableArray());
        public SpecializedType SubstituteGenerics(
            ImmutableArray<GenericParameter> parameters,
            ImmutableArray<TypeReference> arguments) =>
            this.TypeArguments.Length == 0 || parameters.Length == 0 ? this :
            With(typeArguments: this.TypeArguments.EagerSelect(t => t.SubstituteGenerics(parameters, arguments)));

        public FmtToken Format() =>
            this.TypeArguments.IsEmpty
                ? FmtToken.Single(this.Type, "type")
                : FmtToken.Concat(this.Type, FmtToken.FormatArray(this.TypeArguments, "<", ">"))
                          .WithTokenNames("type", "genericParameters");

    }
}
