using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class SpecializedType
    {
        public SpecializedType(TypeSignature type, params TypeReference[] genericArgs)
            : this(type, genericArgs.ToImmutableArray()) {}
    }
}
