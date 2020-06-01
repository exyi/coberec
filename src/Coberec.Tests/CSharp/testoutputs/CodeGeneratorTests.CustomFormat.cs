using Coberec.CoreLib;
using System;
using System.Collections.Immutable;

namespace GeneratedProject.ModelNamespace
{
	public sealed class A : ITokenFormatable, ITraversableObject, IEquatable<A>
	{
		public string H {
			get;
		}

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("h");

		int ITraversableObject.PropertyCount => 1;

		private A(NoNeedForValidationSentinel _, string h)
		{
			H = h;
		}

		public A(string h)
			: this(default(NoNeedForValidationSentinel), h)
		{
			ValidateObject(this).ThrowErrors("Could not initialize a due to validation errors", this);
		}

		private static ValidationErrors ValidateObject(A obj)
		{
			return BasicValidators.NotNull(obj.H).Nest("h");
		}

		public static ValidationResult<A> Create(string h)
		{
			A result = new A(default(NoNeedForValidationSentinel), h);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return (propIndex == 0) ? H : null;
		}

		public override int GetHashCode()
		{
			return H.GetHashCode();
		}

		public bool Equals(A b)
		{
			return (object)this == b || ((object)b != null && H == b.H);
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

		public ValidationResult<A> With(string h)
		{
			return (H == h) ? ValidationResult.Create(this) : Create(h);
		}
	}
}

