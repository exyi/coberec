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
			string pString = "5";
			return int.Parse("123456789" + pString);
		}
	}
	public class E
	{
		public static void M(string pString1)
		{
			int.Parse("123456789" + pString1);
		}
	}
	public class C3
	{
		public static void M(string pString1)
		{
			string text = "123456789" + pString1;
		}
	}
}

