using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Xunit;

namespace Coberec.ExprCS
{
    public partial class MethodParameter
    {
        public MethodParameter(TypeReference type, string name)
            : this(type, name, false, null) { }

        public MethodParameter SubstituteGenerics(
            IEnumerable<GenericParameter> parameters,
            IEnumerable<TypeReference> arguments) =>
            SubstituteGenerics(parameters.ToImmutableArray(), arguments.ToImmutableArray());
        public MethodParameter SubstituteGenerics(
            ImmutableArray<GenericParameter> parameters,
            ImmutableArray<TypeReference> arguments) =>
            this.With(type: this.Type.SubstituteGenerics(parameters, arguments));

        public override string ToString()
        {
            var b = $"{this.Name}: {this.Type}";
            if (this.HasDefaultValue)
            {
                b += $" = ";
                if (this.DefaultValue == null)
                    b += this.Type.IsReferenceType == true ? "null" : "default";
                else
                    b += this.DefaultValue;
            }
            return b;
        }
    }
}
