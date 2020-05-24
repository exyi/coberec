using System;
using System.Linq;
using System.Collections.Generic;

namespace Coberec.ExprCS
{
    /// <summary> Type reference that represents an array `Type[...Dimensions]` </summary>
    public partial class ArrayType
    {
        public override string ToString() => $"{this.Type}[{new string(',', this.Dimensions - 1)}]";
    }
}
