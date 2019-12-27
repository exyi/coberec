using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using CheckTestOutput;
using Coberec.CSharpGen;

namespace Coberec.ExprCS.Tests
{
    public static class CheckExtensions
    {
        static string FormatException(Exception exception)
        {
            var topFrame = new EnhancedStackTrace(exception).FirstOrDefault(m => !m.MethodInfo.DeclaringType.Namespace.StartsWith("DotVVM.Framework.Binding"))?.MethodInfo.ToString();
            var msg = $"{exception.GetType().Name} occurred: {exception.Message}";
            if (topFrame != null) msg += $"\n    at {topFrame}";
            if (exception is AggregateException aggregateException && aggregateException.InnerExceptions.Count > 1)
            {
                var inner = aggregateException.InnerExceptions.Select(FormatException).StringJoin("\n\n");
                return $"{msg}\n\n{inner}";
            }
            else
            {
                var inner = exception.InnerException?.Apply(FormatException);

                if (inner == null)
                    return msg;
                else
                    return $"{msg}\n\n{inner}";
            }
        }

        public static void CheckException(this OutputChecker check, Action action, string checkName = null, string fileExtension = "txt", [CallerMemberName] string memberName = null, [CallerFilePath] string sourceFilePath = null)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                var error = FormatException(exception);
                check.CheckString(error, checkName, fileExtension, memberName, sourceFilePath);
                return;
            }
            throw new Exception("Expected test to fail.");
        }

        public static void CheckOutput(this OutputChecker check, MetadataContext cx, string checkName = null, [CallerMemberName] string memberName = null, [CallerFilePath] string sourceFilePath = null)
        {
            check.CheckString(cx.EmitToString(), checkName, "cs", memberName, sourceFilePath);
        }
    }
}
