namespace NS
{
	public class C
	{
		public static int M(string pString1)
		{
			return int.Parse("123456789" + pString1);
		}
	}
	public class D
	{
		public static int M()
		{
			string pString1 = "5";
			return int.Parse("123456789" + pString1);
		}
	}
	public class E
	{
		public static void M(string pString1)
		{
			int.Parse("123456789" + pString1);
		}
	}
	public class F
	{
		public static void M(string pString1)
		{
			_ = "123456789" + pString1;
		}
	}
}

