using System;
using System.Linq;
using System.Collections.Generic;
using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    public partial class ArrayType
    {
        public FmtToken Format() =>
            FmtToken.Concat(this.Type, "[" + new string(',', this.Dimensions - 1) + "]")
            .WithTokenNames(new [] { "type", "" });
    }
}
