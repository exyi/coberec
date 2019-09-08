using System;

namespace MyNs
{
	public struct MyStruct
	{
		public Guid id;

		public int count;

		public readonly int ROField;
	}
	public class D
	{
		public static int M(ref MyStruct @this)
		{
			return @this.ROField;
		}
	}
}

