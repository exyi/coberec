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
		public static string M(ValueTuple<string, int> pTuple)
		{
			return pTuple.Item1;
		}
	}
	public class E
	{
		public static void M(ValueTuple<string, int> pTuple)
		{
			pTuple.Item1 = null;
		}
	}
	public class F
	{
		public static ImmutableArray<int> M()
		{
			return ImmutableArray<int>.Empty;
		}
	}
	public class G
	{
		public static void M(ValueTuple<string, int> pTuple)
		{
			pTuple.Item2++;
		}
	}
}

