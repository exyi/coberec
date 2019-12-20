using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
    /// <summary> Represents a reference to a property. The generic parameters of the parent class are substituted - this class is basically <see cref="PropertySignature" /> + generic arguments </summary>
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
        public string Name() => Signature.Name;
    }
}
