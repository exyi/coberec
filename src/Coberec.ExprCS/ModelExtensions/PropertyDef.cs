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

        /// <summary> Creates a property definition with the specified getter and setter factories. In principle, this method is similar to <see cref="MethodDef.Create(MethodSignature, Func{ParameterExpression, Expression})" /> </summary>
        public static PropertyDef Create(
            PropertySignature signature,
            Func<ParameterExpression, Expression> getter,
            Func<ParameterExpression, ParameterExpression, Expression> setter = null) =>
            new PropertyDef(
                signature,
                getter == null ? null : MethodDef.Create(signature.Getter, getter),
                setter == null ? null : MethodDef.Create(signature.Setter, setter)
            );

        /// <summary> Creates an empty property definition. Useful when declaring an interface. </summary>
        public static PropertyDef InterfaceDef(PropertySignature signature, XmlComment doccomment = null) =>
            new PropertyDef(signature, signature.Getter?.Apply(MethodDef.InterfaceDef), signature.Setter?.Apply(MethodDef.InterfaceDef), ImmutableArray<PropertyReference>.Empty, doccomment);

        /// <summary> Marks the property definition as implementation of the specified interface properties. </summary>
        public PropertyDef AddImplements(params PropertyReference[] interfaceMethods) =>
            this.With(implements: this.Implements.AddRange(interfaceMethods));
    }
}
