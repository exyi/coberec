using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MyNamespace
{
	public class MyType
	{
		public readonly string F1;

		private readonly string[] F2;

		internal readonly ValueTuple<List<int>, Task> F3;

		protected internal static string[] F4;
	}
}

