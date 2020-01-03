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
		public static string M(string s)
		{
			return s + "";
		}
	}
	public class E
	{
		public static string M(string s)
		{
			return s + s;
		}
	}
	public class F
	{
		public static string M(string s)
		{
			return s + "; " + s;
		}
	}
	public class G
	{
		public static string M(string s)
		{
			return s + "; " + s + "; " + s;
		}
	}
	public class H
	{
		public static string M(string s)
		{
			return string.Concat(s, "; ", s + "; " + s + "; " + s, "; ", s);
		}
	}
}

