using System;

namespace NS
{
	public class C
	{
		public static Func<int> M()
		{
			return () => 1;
		}
	}
	public class D
	{
		public static Func<object> M()
		{
			return () => "abc";
		}
	}
	public class E
	{
		public static Func<bool, object> M()
		{
			return (bool pBool1) => pBool1 ? ((object)((Func<int>)(() => 1))()) : null;
		}
	}
}

