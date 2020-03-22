using System;

namespace NS
{
	public class C
	{
		public static string M(string pString1)
		{
			return (pString1 == null) ? "<empty>" : pString1;
		}
	}
	public class D
	{
		public static void M(int p1)
		{
			if (p1 > 10)
			{
				Console.WriteLine(p1);
			}
		}
	}
	public class E
	{
		public static void M(int p1)
		{
			if (p1 > 10)
			{
				Console.WriteLine(p1);
			}
		}
	}
}

