using System;

namespace NS
{
	public class C
	{
		public static Func<string, bool> M(Predicate<string> predicateP)
		{
			return (string obj) => predicateP(obj);
		}
	}
}

