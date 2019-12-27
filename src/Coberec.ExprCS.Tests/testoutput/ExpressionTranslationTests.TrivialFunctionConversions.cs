using System;

namespace NS
{
	public class C
	{
		public static Func<object> M(Func<string> stringFunc)
		{
			return stringFunc;
		}
	}
	public class D
	{
		public static Func<object> M(Func<string> stringFunc)
		{
			return stringFunc;
		}
	}
	public class E
	{
		public static Func<string> M(Func<string> stringFunc)
		{
			return stringFunc;
		}
	}
	public class F
	{
		public static Func<object> M(Func<string> stringFunc)
		{
			return stringFunc;
		}
	}
}

