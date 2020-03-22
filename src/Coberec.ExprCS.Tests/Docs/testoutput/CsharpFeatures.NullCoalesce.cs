using System;

namespace NS
{
	public class C
	{
		public static string M(string pString1)
		{
			return (pString1 != null) ? pString1 : "<null>";
		}
	}
	public class D
	{
		public static string M()
		{
			string tmp = Console.ReadLine();
			return (tmp != null) ? tmp : "<null>";
		}
	}
	public class E
	{
		public static int? M(int? pNullInt)
		{
			return pNullInt.HasValue ? pNullInt : new int?(-1);
		}
	}
}

