using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.CSharpGen
{
    public class ValidatorConfig
    {
        public ValidatorConfig(
            string validationMethodName,
            IEnumerable<(string name, int parameterIndex, object defaultValue)> validatorParameters,
            bool acceptsNull = true
        )
        {
            this.ValidationMethodName = validationMethodName;
            this.ValidatorParameters = validatorParameters.ToImmutableArray();
            this.AcceptsNull = acceptsNull;
        }

        /// Method that takes a value in a parameter named `value` and returns `ValidationErrors`
        public string ValidationMethodName { get; }
        public ImmutableArray<(string name, int parameterIndex, object defaultValue)> ValidatorParameters { get; }
        /// Whether this validator can be invoked with null values or should be null-guarded
        public bool AcceptsNull { get; }
    }
}
