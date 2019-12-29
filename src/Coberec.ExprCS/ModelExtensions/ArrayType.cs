using System;
using System.Linq;
using System.Collections.Generic;

namespace Coberec.ExprCS
{
    public partial class ArrayType
    {
        public override string ToString() => $"{this.Type}[{new string(',', this.Dimensions - 1)}]";
    }
}
