namespace NS
{
	public class C
	{
		public static bool M(string pString1)
		{
			return (object)"abcd" == pString1;
		}
	}
	public class D
	{
		public static bool M(string pString1)
		{
			return null == pString1;
		}
	}
	public class E
	{
		public static bool M(string pString1)
		{
			return null != pString1;
		}
	}
	public class F
	{
		public static bool M(bool pBool1)
		{
			return pBool1;
		}
	}
	public class G
	{
		public static bool M()
		{
			return 1 == 0;
		}
	}
	public class H
	{
		public static bool M()
		{
			return 1 == 1;
		}
	}
	public class I
	{
		public static bool M()
		{
			return 1 == 1;
		}
	}
	public class J
	{
		public static bool M()
		{
			return 1L == 1L;
		}
	}
	public class K
	{
		public static bool M()
		{
			return 1f == 1f;
		}
	}
	public class L
	{
		public static bool M()
		{
			return 1.0 == 1.0;
		}
	}
	public class M
	{
		public static bool M2()
		{
			return 1.0 != 1.0;
		}
	}
}

