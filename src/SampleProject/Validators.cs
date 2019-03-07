using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.CoreLib;

namespace SampleProject
{
    public static class Validators
    {
        public static ValidationErrors LengthDivisibleBy(GeneratedSchema.TypeB value, int num)
        {
            // if (value.Length % num != 0)
            //     return ValidationErrors.Create($"String length must be divisible by {num}.");
            // else
                return null;
        }
    }
}
