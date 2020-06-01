using System.Collections.Immutable;

namespace NS
{
	public class C
	{
		public static object M(bool pBool1)
		{
			return pBool1 ? ((object)0) : null;
		}
	}
	public class D
	{
		public static object M(bool pBool1)
		{
			return pBool1 ? ((object)0) : "";
		}
	}
	public class E
	{
		public static object M(bool pBool1, ImmutableArray<string> pImmutableArray)
		{
			return pBool1 ? ((object)pImmutableArray) : null;
		}
	}
}

