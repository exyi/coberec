using System;

namespace MyNs
{
	public struct MyStruct : ICloneable
	{
		public Guid id;

		public int count;

		public object Clone()
		{
			return this;
		}
	}
	public class D
	{
		public static object M(MyStruct p)
		{
			MyStruct myStruct = p;
			return myStruct.Clone();
		}
	}
	public class E
	{
		public static object M(MyStruct p)
		{
			return ((ICloneable)p).Clone();
		}
	}
}

