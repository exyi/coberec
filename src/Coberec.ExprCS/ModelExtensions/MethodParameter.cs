using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Coberec.ExprCS
{
    public partial class MethodParameter
    {
        public MethodParameter(TypeReference type, string name)
            : this(type, name, false, null) { }


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
