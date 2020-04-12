using System;

namespace NS
{
	public class C
	{
		public static int M(UriBuilder pUriBuilder)
		{
			return pUriBuilder.Port;
		}
	}
	public class D
	{
		public static void M(UriBuilder pUriBuilder)
		{
			pUriBuilder.Port = 8080;
		}
	}
	public class E
	{
		public static void M(UriBuilder pUriBuilder)
		{
			pUriBuilder.Port += 10;
		}
	}
}

