using System;
using Coberec.CoreLib;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class NamespaceSignature
    {
        public string FullName() => Parent == null ? Name : $"{Parent.FullName()}.{Name}";
        public static NamespaceSignature System = new NamespaceSignature("System", null);
    }
}
