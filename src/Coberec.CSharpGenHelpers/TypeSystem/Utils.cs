using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;

namespace Coberec.CSharpGen.TypeSystem
{
    public static class Utils
    {
        public static INamespace FindDescendantNamespace(this INamespace ns, string nsName)
        {
            var xs = nsName.Split('.');
            foreach (var x in xs)
            {
                ns = ns?.GetChildNamespace(x);
            }
            return ns;
        }
    }
}
