using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler.IL;

namespace Coberec.ExprCS.CodeTranslation
{
    static class ILAstHelpers
    {
        public static void AddLeaveInstruction(this Block b, Block nextBlock)
        {
            if (b.HasReachableEndpoint())
                b.Instructions.Add(new Branch(nextBlock));
        }

        public static bool HasReachableEndpoint(this Block b)
        {
            return b.Instructions.LastOrDefault()?.HasFlag(InstructionFlags.EndPointUnreachable) != true;
        }
    }
}
