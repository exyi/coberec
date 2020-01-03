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
				return pBool1 ? ((object)(object)f()) : null;
			}
		}
	}
}

