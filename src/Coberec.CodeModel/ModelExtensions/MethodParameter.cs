using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Coberec.CoreLib;
using Xunit;

namespace Coberec.ExprCS
{
    public partial class MethodParameter
    {
		static partial void ValidateObjectExtension(ref ValidationErrorsBuilder e, MethodParameter p)
        {
            if (p.Type == TypeSignature.Void)
                e.AddErr($"Method parameter must not have a type void.", "type");
        }


        public MethodParameter(TypeReference type, string name)
            : this(type, name, false, null, isParams: false) { }

        public MethodParameter SubstituteGenerics(
            IEnumerable<GenericParameter> parameters,
            IEnumerable<TypeReference> arguments) =>
            SubstituteGenerics(parameters.ToImmutableArray(), arguments.ToImmutableArray());
        public MethodParameter SubstituteGenerics(
            ImmutableArray<GenericParameter> parameters,
            ImmutableArray<TypeReference> arguments) =>
            this.With(type: this.Type.SubstituteGenerics(parameters, arguments));

        public MethodParameter WithDefault(object defaultValue) =>
            this.With(hasDefaultValue: true, defaultValue: defaultValue);

        public FmtToken Format()
        {
            var b = FmtToken.Concat(
                this.IsParams ? "params " : "",
                this.Name,
                ": ",
                this.Type
            )
                            .WithTokenNames("isParams", "name", "", "type");
            if (this.HasDefaultValue)
            {
                string def;
                if (this.DefaultValue == null)
                    def = this.Type.IsReferenceType == true ? "null" : "default";
                else
                    def = this.DefaultValue.ToString();

                b = FmtToken.Concat(b, " = ", def).WithTokenNames(null, "hasDefaultValue", "defaultValue");
            }
            return b;
        }
    }
}
