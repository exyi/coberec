using Coberec.CoreLib;
using System;

namespace GeneratedProject.ModelNamespace
{
	public interface B
	{
	}
	public sealed class A : B, IEquatable<A>
	{
		public static ValidationResult<A> Create()
		{
			A result = new A();
			return ValidationResult.Create(result);
		}

		public override string ToString()
		{
			return "a {}";
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
	}
	public interface X
	{
		B P {
			get;
		}
	}
	public sealed class Y : X, IEquatable<Y>
	{
		public A P {
			get;
		}

		B X.P => P;

		private Y(NoNeedForValidationSentinel _, A p)
		{
			P = p;
		}

		public Y(A p)
			: this(default(NoNeedForValidationSentinel), p)
		{
			ValidateObject(this).ThrowErrors("Could not initialize y due to validation errors", this);
		}

		private static ValidationErrors ValidateObject(Y obj)
		{
			return BasicValidators.NotNull(obj.P).Nest("p");
		}

		public static ValidationResult<Y> Create(A p)
		{
			Y result = new Y(default(NoNeedForValidationSentinel), p);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override string ToString()
		{
			return "y {p = " + P.ToString() + "}";
		}

		public override int GetHashCode()
		{
			return P.GetHashCode();
		}

		public bool Equals(Y b)
		{
			return (object)this == b || ((object)b != null && P == b.P);
		}

		public static bool operator ==(Y a, Y b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(Y a, Y b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as Y);
		}

		public ValidationResult<Y> With(A p)
		{
			return (P == p) ? ValidationResult.Create(this) : Create(p);
		}
	}
}
