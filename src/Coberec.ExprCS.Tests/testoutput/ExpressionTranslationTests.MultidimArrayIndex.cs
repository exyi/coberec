namespace NS
{
	public class C
	{
		public static string M(string[,,] a)
		{
			return a[11, 12, 13];
		}
	}
	public class D
	{
		public static void M(string[,,] a)
		{
			a[11, 12, 13] = "abc";
		}
	}
}

