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

        public override string ToString() =>
            this.GenericParameters.IsEmpty
                ? this.Type.ToString()
                : $"{this.Type}<{string.Join(", ", GenericParameters)}>";

    }
}
