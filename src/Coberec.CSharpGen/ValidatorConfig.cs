using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Coberec.CSharpGen
{
    public class ValidatorConfig
    {
        public ValidatorConfig(
            string validationMethodName,
            IEnumerable<(string name, int parameterIndex, JToken defaultValue)> validatorParameters,
            bool acceptsNull = true
        )
        {
            this.ValidationMethodName = validationMethodName;
            this.ValidatorParameters = validatorParameters?.ToImmutableArray() ?? ImmutableArray<(string name, int parameterIndex, JToken defaultValue)>.Empty;
            this.AcceptsNull = acceptsNull;
        }

        /// Method that takes a value in a parameter named `value` and returns `ValidationErrors`
        public string ValidationMethodName { get; }
        /// Indicate which parameters that the validated value/s. When null it's expected to be parameter named `value`
        public ImmutableArray<int>? ValueParameters { get; }
        public ImmutableArray<(string name, int parameterIndex, JToken defaultValue)> ValidatorParameters { get; }
        /// Whether this validator can be invoked with null values or should be null-guarded
        public bool AcceptsNull { get; }
    }
}
