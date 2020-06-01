using System;

namespace NS
{
	public class C
	{
		public static object M()
		{
			return ((Func<bool, object>)((bool pBool1) => pBool1 ? ((object)((Func<int>)(() => 1))()) : null))(arg: true);
		}
	}
}

