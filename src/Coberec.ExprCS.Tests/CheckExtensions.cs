using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CheckTestOutput;

namespace Coberec.ExprCS.Tests
{
    public static class CheckExtensions
    {
        public static void CheckOutput(this OutputChecker check, MetadataContext cx, string checkName = null, [CallerMemberName] string memberName = null, [CallerFilePath] string sourceFilePath = null)
        {
            check.CheckString(cx.EmitToString(), checkName, "cs", memberName, sourceFilePath);
        }
    }
}
