using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
    /// <summary> A type reference to a signature with filled in type parameters. This class is basically <see cref="TypeSignature" /> + generic arguments </summary>
    public partial class SpecializedType
    {
        public SpecializedType(TypeSignature type, params TypeReference[] genericArgs)
            : this(type, genericArgs.ToImmutableArray()) {}

        /// <summary> Gets a declaring type (parent type) if it exists. If the type is global (directly in a namespace), `null` is returned. </summary>
        public SpecializedType DeclaringType() =>
            this.Type.Parent.Match(
                ns => null,
                t => t.Specialize(this.GenericParameters.Take(t.TypeParameters.Length).ToImmutableArray())
            );

        public override string ToString() =>
            this.GenericParameters.IsEmpty
                ? this.Type.ToString()
                : $"{this.Type}<{string.Join(", ", GenericParameters)}>";

    }
}
