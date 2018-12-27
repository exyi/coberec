using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.CoreLib
{
    public static class BasicValidators
    {
        public static ValidationErrors NotNull(object value) =>
            value == null ? ValidationErrors.Create("Object can not be null") : null;
        public static ValidationErrors Range(int low, int high, int value) =>
            value >= low && value <= low ? null : ValidationErrors.Create($"Integer value {value} is out of range [{low}, {high}]");
        public static ValidationErrors NotEmpty(IEnumerable<object> value) =>
            value.Any() ? null : ValidationErrors.Create("Object can not be empty.");
    }
}
