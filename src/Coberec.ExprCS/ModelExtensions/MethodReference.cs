using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;

namespace Coberec.ExprCS
{
    public partial class MethodReference
    {
        public SpecializedType DeclaringType() => new SpecializedType(this.Signature.DeclaringType, this.TypeParameters);
        public TypeReference ResultType() => Signature.ResultType.SubstituteGenerics(Signature.TypeParameters, this.MethodParameters);
        public ImmutableArray<MethodParameter> Params() =>
            Signature.Params.EagerSelect(p => p.SubstituteGenerics(Signature.TypeParameters, this.MethodParameters)
                                               .SubstituteGenerics(Signature.DeclaringType.TypeParameters, this.TypeParameters));
        public string Name() => Signature.Name;

        public override string ToString() =>
            MethodSignature.ToString(Signature, this.MethodParameters, this.Params(), this.ResultType());
    }
}
