using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;

namespace Coberec.ExprCS.Tests
{
    public class RaiseExceptionDebugProvider//: System.Diagnostics.DebugProvider
    {
        public static void HackIt()
        {
            if (Debugger.IsAttached) return;
            var x = Type.GetType("System.Diagnostics.DebugProvider, System.Private.CoreLib");
            var field = x.GetField("s_FailCore", BindingFlags.Static | BindingFlags.NonPublic);
            Action<string, string, string, string> fail = (a, b, c, d) => throw new Exception($"Assertion failed ({a} {b} {c} {d})");
            field.SetValue(null, fail);
        }
    }
}
