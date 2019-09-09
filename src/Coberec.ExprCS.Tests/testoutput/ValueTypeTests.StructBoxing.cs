using System;

namespace MyNs
{
	public struct MyStruct
	{
		public Guid id;

		public int count;
	}
	public class D
	{
		public static object M(MyStruct p)
		{
			return p;
		}
	}
	public class E
	{
		public static int M(MyStruct p)
		{
			return p.GetHashCode();
		}
	}
}

