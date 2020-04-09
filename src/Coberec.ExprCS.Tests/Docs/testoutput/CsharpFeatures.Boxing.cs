using System;
using System.Collections.Generic;

namespace NS
{
	public class C
	{
		public static object M(TimeSpan pTime)
		{
			return pTime;
		}
	}
	public class D
	{
		public static IEquatable<TimeSpan> M(TimeSpan pTime)
		{
			return pTime;
		}
	}
	public class E
	{
		public static object M(string pString1)
		{
			return pString1;
		}
	}
	public class F
	{
		public static IEnumerable<char> M(string pString1)
		{
			return pString1;
		}
	}
}

