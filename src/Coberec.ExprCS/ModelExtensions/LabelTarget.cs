using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class LabelTarget
    {
        public static LabelTarget New(string name) =>
            new LabelTarget(Guid.NewGuid(), name, TypeSignature.Void);

        public static LabelTarget New(string name, TypeReference type) =>
            new LabelTarget(Guid.NewGuid(), name, type);
    }
}
