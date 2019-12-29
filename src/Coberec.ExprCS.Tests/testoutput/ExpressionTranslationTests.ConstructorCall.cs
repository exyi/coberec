using System;
using System.Collections.Generic;

namespace NS
{
	public class C
	{
		public static List<string> M()
		{
			return new List<string>();
		}
	}
	public class D
	{
		public static List<string> M()
		{
			return new List<string>(1234);
		}
	}
	public class E
	{
		public static DateTime M()
		{
			return new DateTime(1234L);
		}
	}
}

