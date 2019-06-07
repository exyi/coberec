using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class ParameterExpression
    {
        public static ParameterExpression Create(TypeReference type, string name) => new ParameterExpression(Guid.NewGuid(), name, type, mutable: false);
        public static ParameterExpression CreateMutable(TypeReference type, string name) => new ParameterExpression(Guid.NewGuid(), name, type, mutable: true);
    }
}
