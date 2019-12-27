using System.Collections.Generic;

namespace NS
{
	public class C
	{
		public static IEnumerable<int>[][] M()
		{
			return new IEnumerable<int>[12][];
		}
	}
	public class D
	{
		public static string[,,][,,,,] M()
		{
			return new string[11, 12, 13][,,,,];
		}
	}
}

