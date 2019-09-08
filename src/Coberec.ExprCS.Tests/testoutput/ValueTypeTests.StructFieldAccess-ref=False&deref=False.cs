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
		public static void M(MyStruct @this)
		{
			MyStruct myStruct = @this;
			myStruct.count = 0;
		}
	}
}

