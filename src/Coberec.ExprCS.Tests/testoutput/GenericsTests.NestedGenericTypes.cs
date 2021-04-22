using System;
using System.Collections.Generic;

namespace MyNamespace
{
	public class MyType<T1>
	{
		public class MyNestedType<T2>
		{
			public T1 A { get; }

			public static T2 B { get; }

			public static Dictionary<T1, T2> C { get; set; }

			public ValueTuple<T1, string, T2> D { get; set; }

			public MyNestedType<T2> E { get; set; }
		}
	}
}

