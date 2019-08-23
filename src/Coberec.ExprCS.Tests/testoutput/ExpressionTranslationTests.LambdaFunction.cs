using System;

namespace NS
{
	public class C
	{
		public static object M()
		{
			return ((Func<bool, object>)delegate(bool pBool1)
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
			})(arg: true);
		}
	}
}

