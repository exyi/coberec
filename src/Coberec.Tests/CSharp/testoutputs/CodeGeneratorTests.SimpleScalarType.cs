using Coberec.CoreLib;
using System;

namespace GeneratedProject.ModelNamespace
{
	public sealed class Scalar123 : IEquatable<Scalar123>
	{
		public string Value {
			get;
		}

		private Scalar123(NoNeedForValidationSentinel _, string value)
		{
			Value = value;
		}

		public Scalar123(string value)
			: this(default(NoNeedForValidationSentinel), value)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Scalar123 due to validation errors");
		}

		private static ValidationErrors ValidateObject(Scalar123 obj)
		{
			return BasicValidators.NotNull(obj.Value).Nest("value");
		}

		public static ValidationResult<Scalar123> Create(string value)
		{
			Scalar123 result = new Scalar123(default(NoNeedForValidationSentinel), value);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public bool Equals(Scalar123 b)
		{
			return (object)this == b || ((object)b != null && Value == b.Value);
		}

		public static bool operator ==(Scalar123 a, Scalar123 b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(Scalar123 a, Scalar123 b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as Scalar123);
		}
	}
}

