using System;
using System.Collections.Immutable;
using Coberec.CoreLib;

namespace GeneratedProject.ModelNamespace
{
	public interface B
	{
		string A { get; }

		ValidationResult<B> With(string a);
	}
	public sealed class A : B, ITokenFormatable, ITraversableObject, IEquatable<A>
	{
		public string A2 { get; }

		string B.A => A2;

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("a");

		int ITraversableObject.PropertyCount => 1;

		private A(NoNeedForValidationSentinel _, string a)
		{
			A2 = a;
		}

		public A(string a)
			: this(default(NoNeedForValidationSentinel), a)
		{
			ValidateObject(this).ThrowErrors("Could not initialize a due to validation errors", this);
		}

		private static ValidationErrors ValidateObject(A obj)
		{
			return BasicValidators.NotNull(obj.A2).Nest("a");
		}

		public static ValidationResult<A> Create(string a)
		{
			A result = new A(default(NoNeedForValidationSentinel), a);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create((object)"a {a = ", (object)A2, (object)"}"), new string[3] { "", "a", "" });
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return (propIndex == 0) ? A2 : null;
		}

		public override int GetHashCode()
		{
			return A2.GetHashCode();
		}

		public bool Equals(A b)
		{
			return (object)this == b || ((object)b != null && A2 == b.A2);
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

		public ValidationResult<A> With(string a)
		{
			return (A2 == a) ? ValidationResult.Create(this) : Create(a);
		}

		ValidationResult<B> B.With(string a)
		{
			return With(a).Cast<B>();
		}
	}
}

