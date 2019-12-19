using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;

namespace Coberec.ExprCS
{
	/// <summary> Represents a complete definition of a property. Apart from the (<see cref="TypeDef.Signature" />) contains the implementation (<see cref="Getter" />, <see cref="Setter" />) and attributes </summary>
    public partial class PropertyDef
    {
        public PropertyDef(PropertySignature signature, MethodDef getter, MethodDef setter)
            : this(signature, getter, setter, ImmutableArray<PropertyReference>.Empty) { }

        /// <summary> Creates an empty property definition. Useful when declaring an interface. </summary>
        public static PropertyDef InterfaceDef(PropertySignature signature) =>
            new PropertyDef(signature, signature.Getter?.Apply(MethodDef.InterfaceDef), signature.Setter?.Apply(MethodDef.InterfaceDef));
    }
}
