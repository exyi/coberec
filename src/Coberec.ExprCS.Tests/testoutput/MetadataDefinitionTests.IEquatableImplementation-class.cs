using System;

namespace MyNamespace
{
	public class MyType : IEquatable<MyType>
	{
		public bool Equals(MyType obj)
		{
			return true;
		}
	}
}

