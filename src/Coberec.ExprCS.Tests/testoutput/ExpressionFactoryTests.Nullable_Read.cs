namespace NS
{
	public class C
	{
		public static bool M(int? a)
		{
			return a.HasValue;
		}
	}
	public class D
	{
		public static int M(int? a)
		{
			return a.Value;
		}
	}
}

