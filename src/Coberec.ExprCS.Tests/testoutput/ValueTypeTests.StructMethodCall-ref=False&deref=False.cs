using System;

namespace NS
{
	public struct MyStruct
	{
		public Guid id;

		public int count;

		public void SomeMethod()
		{
		}
	}
	public class D
	{
		public static void M(MyStruct @this)
		{
			MyStruct myStruct = @this;
			myStruct.SomeMethod();
		}
	}
}

