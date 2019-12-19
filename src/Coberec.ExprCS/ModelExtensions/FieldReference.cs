using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    /// <summary> Represents a reference to a field. The generic parameters of the parent class are substituted - this class is basically <see cref="FieldSignature" /> + generic arguments </summary>
    public partial class FieldReference
    {
        public SpecializedType DeclaringType() => new SpecializedType(this.Signature.DeclaringType, this.TypeParameters);
        public TypeReference ResultType() => Signature.ResultType.SubstituteGenerics(Signature.DeclaringType.TypeParameters, this.TypeParameters);
    }
}
