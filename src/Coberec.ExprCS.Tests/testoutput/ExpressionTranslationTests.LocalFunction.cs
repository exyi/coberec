namespace NS
{
	public class C
	{
		public static object M()
		{
			return f2(pBool1: true);
			int f()
			{
				return 1;
			}
			object f2(bool pBool1)
			{
				object result2;
				if (pBool1)
				{
					result2 = (object)(object)f();
				}
				else
				{
					result2 = null;
				}
				return result2;
			}
		}
	}
}

