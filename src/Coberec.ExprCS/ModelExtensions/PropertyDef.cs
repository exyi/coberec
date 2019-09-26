using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;

namespace Coberec.ExprCS
{
    public partial class PropertyDef
    {
        public PropertyDef(PropertySignature signature, MethodDef getter, MethodDef setter)
            : this(signature, getter, setter, ImmutableArray<PropertySignature>.Empty) { }

        public static PropertyDef InterfaceDef(PropertySignature signature) =>
            new PropertyDef(signature, signature.Getter?.Apply(MethodDef.InterfaceDef), signature.Setter?.Apply(MethodDef.InterfaceDef));
    }
}
