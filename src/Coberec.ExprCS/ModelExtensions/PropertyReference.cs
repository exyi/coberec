using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class PropertyReference
    {
        public SpecializedType DeclaringType() => new SpecializedType(this.Signature.DeclaringType, this.TypeParameters);
        public TypeReference Type() => Signature.Type.SubstituteGenerics(Signature.DeclaringType.TypeParameters, this.TypeParameters);
        public MethodReference Getter() =>
            Signature.Getter == null ? null :
            new MethodReference(Signature.Getter, this.TypeParameters, ImmutableArray<TypeReference>.Empty);
        public MethodReference Setter() =>
            Signature.Setter == null ? null :
            new MethodReference(Signature.Setter, this.TypeParameters, ImmutableArray<TypeReference>.Empty);
    }
}
