using Coberec.CoreLib;
using System;

namespace GeneratedProject.ModelNamespace
{
	public interface B
	{
		ValidationResult<B> With();
	}
	public sealed class A : B, IEquatable<A>
	{
		public static ValidationResult<A> Create()
		{
			A result = new A();
			return ValidationResult.Create(result);
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

