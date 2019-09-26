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
    }
}
