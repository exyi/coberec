using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class TypeReference
    {
        public static implicit operator TypeReference(TypeSignature signature) => new SpecializedType(signature, ImmutableArray<TypeReference>.Empty);

        public TypeReference UnwrapReference() =>
            this is ByReferenceTypeCase byref ? byref.Item.Type
                                              : this;
    }
}
