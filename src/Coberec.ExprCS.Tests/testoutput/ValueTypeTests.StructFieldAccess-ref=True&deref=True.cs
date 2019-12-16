using System;

namespace NS
{
	public struct MyStruct
	{
		public Guid id;

		public int count;
	}
	public class D
	{
		public static void M(ref MyStruct @this)
		{
			MyStruct myStruct = @this;
			myStruct.count = 0;
		}
	}
}

