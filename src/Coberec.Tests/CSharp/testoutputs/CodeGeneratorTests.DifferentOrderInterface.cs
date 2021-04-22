using System;
using System.Collections.Immutable;
using Coberec.CoreLib;

namespace GeneratedProject.ModelNamespace
{
	public interface B
	{
		ValidationResult<B> With();
	}
	public sealed class A : B, ITokenFormatable, ITraversableObject, IEquatable<A>
	{
		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray<string>.Empty;

		int ITraversableObject.PropertyCount => 0;

		public static ValidationResult<A> Create()
		{
			A result = new A();
			return ValidationResult.Create(result);
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create((object)"a {}"), new string[1] { "" });
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return null;
		}

		public override int GetHashCode()
		{
			return 42;
		}

		public bool Equals(A b)
		{
			return (object)this == b || (((object)b != null) ? true : false);
		}

		public static bool operator ==(A a, A b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(A a, A b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as A);
		}

		public ValidationResult<A> With()
		{
			return true ? ValidationResult.Create(this) : Create();
		}

		ValidationResult<B> B.With()
		{
			return With().Cast<B>();
		}
	}
}

