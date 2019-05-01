using Coberec.CoreLib;
using System;

namespace Coberec.ExprCS
{
	public sealed partial class NamespaceSignature
	{
		public string FullName() => Parent == null ? Name : $"{Parent.FullName()}.{Name}";
	}
}
