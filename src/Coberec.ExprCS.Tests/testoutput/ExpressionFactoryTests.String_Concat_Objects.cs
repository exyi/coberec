namespace NS
{
	public class C
	{
		public static string M()
		{
			return "";
		}
	}
	public class D
	{
		public static string M(long @int)
		{
			return @int + "";
		}
	}
	public class E
	{
		public static string M()
		{
			return "12--";
		}
	}
	public class F
	{
		public static string M(long @int, long? intn)
		{
			return @int + "; " + intn;
		}
	}
}

