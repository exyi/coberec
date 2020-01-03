namespace NS
{
	public class C
	{
		public static ref int M(ref int r1)
		{
			return ref r1;
		}
	}
	public class D
	{
		public static ref int M(ref int r1, ref int r2, bool pBool1)
		{
			return ref pBool1 ? ref r1 : ref r2;
		}
	}
}

