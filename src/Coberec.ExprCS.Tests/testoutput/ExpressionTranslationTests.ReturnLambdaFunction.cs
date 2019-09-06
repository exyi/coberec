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
			return delegate(bool pBool1)
			{
				object result2;
				if (pBool1)
				{
					result2 = (object)(object)((Func<int>)(() => 1))();
				}
				else
				{
					result2 = null;
				}
				return result2;
			};
		}
	}
}

