using System;
using System.Diagnostics.CodeAnalysis;

namespace MyNamespace
{
	public struct MyType : IEquatable<MyType>
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

