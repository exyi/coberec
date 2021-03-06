using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    /// <summary> Special code fragment (not an expression) that represents ID of a <see cref="BreakExpression" /> target. </summary>
    public partial class LabelTarget
    {
        /// <summary> Creates new label target with unique id. Its type is void. </summary>
        public static LabelTarget New(string name) =>
            new LabelTarget(Guid.NewGuid(), name, TypeSignature.Void);

        /// <summary> Creates new label target with unique id. </summary>
        public static LabelTarget New(string name, TypeReference type) =>
            new LabelTarget(Guid.NewGuid(), name, type);
    }
}
