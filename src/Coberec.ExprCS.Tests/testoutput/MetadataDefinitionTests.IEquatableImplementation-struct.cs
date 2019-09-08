using System;

namespace MyNamespace
{
	public struct MyType : IEquatable<MyType>
	{
		public bool Equals(MyType obj)
		{
			return true;
		}
	}
}

