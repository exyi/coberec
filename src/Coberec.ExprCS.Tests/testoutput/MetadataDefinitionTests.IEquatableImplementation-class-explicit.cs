using System;
using System.Diagnostics.CodeAnalysis;

namespace MyNamespace
{
	public class MyType : IEquatable<MyType>
	{
		private bool Equals(MyType obj)
		{
			return true;
		}

		bool IEquatable<MyType>.Equals([AllowNull] MyType other)
		{
			return Equals(other);
		}
	}
}

