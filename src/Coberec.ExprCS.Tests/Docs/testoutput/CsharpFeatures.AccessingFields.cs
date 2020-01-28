using System;
using System.Collections.Immutable;

namespace NS
{
	public class C
	{
		public static string M(ValueTuple<string, int> pTuple)
		{
			ValueTuple<string, int> valueTuple = pTuple;
			return valueTuple.Item1;
		}
	}
	public class D
	{
		public static void M(ValueTuple<string, int> pTuple)
		{
			ValueTuple<string, int> valueTuple = pTuple;
			valueTuple.Item1 = null;
		}
	}
	public class E
	{
		public static ImmutableArray<int> M()
		{
			return ImmutableArray<int>.Empty;
		}
	}
}

