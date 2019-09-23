using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class PropertyDef
    {
        public PropertyDef(PropertySignature signature, MethodDef getter, MethodDef setter)
            : this(signature, getter, setter, ImmutableArray<PropertySignature>.Empty) { }
    }
}
