using System;

namespace NS
{
	public class C
	{
		public static int M()
		{
			return ((Func<int>)(() => 1))();
		}
	}
	public class D
	{
		public static int M()
		{
			return ((Func<bool, int>)((bool a) => a ? 1 : 2))(arg: true);
		}
	}
	public class E
	{
		public static Func<bool, int> M()
		{
			return (bool a) => a ? 1 : 2;
		}
	}
}

