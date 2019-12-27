using System;

namespace NS
{
	public class C
	{
		public static ref string M(string[] a)
		{
			return ref a[11];
		}
	}
	public class D
	{
		public static ref string M(string[,,] a)
		{
			return ref a[11, 12, 13];
		}
	}
	public class E
	{
		public static ref int M(ref int r)
		{
			return ref r;
		}
	}
	public class F
	{
		public static ref int M(ref ValueTuple<int, int> myTuple)
		{
			return ref myTuple.Item1;
		}
	}
}

