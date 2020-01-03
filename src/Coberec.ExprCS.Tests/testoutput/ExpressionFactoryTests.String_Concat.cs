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
			string[] tmpArray2 = new string[5]
			{
				s,
				"; ",
				null,
				null,
				null
			};
			string[] tmpArray = new string[5]
			{
				s,
				"; ",
				s,
				"; ",
				s
			};
			tmpArray2[2] = string.Concat(tmpArray);
			tmpArray2[3] = "; ";
			tmpArray2[4] = s;
			return string.Concat(tmpArray2);
		}
	}
}

