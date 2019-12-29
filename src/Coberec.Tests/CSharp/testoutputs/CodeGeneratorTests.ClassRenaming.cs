using Coberec.CoreLib;
using System;

namespace GeneratedProject.ModelNamespace
{
	public sealed class Equals2 : IEquatable<Equals2>
	{
		public string Value {
			get;
		}

		private Equals2(NoNeedForValidationSentinel _, string value)
		{
			Value = value;
		}

		public Equals2(string value)
			: this(default(NoNeedForValidationSentinel), value)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Equals due to validation errors");
		}

		private static ValidationErrors ValidateObject(Equals2 obj)
		{
			return BasicValidators.NotNull(obj.Value).Nest("value");
		}

		public static ValidationResult<Equals2> Create(string value)
		{
			Equals2 result = new Equals2(default(NoNeedForValidationSentinel), value);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public bool Equals(Equals2 b)
		{
			return (object)this == b || ((object)b != null && Value == b.Value);
		}

		public static bool operator ==(Equals2 a, Equals2 b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(Equals2 a, Equals2 b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as Equals2);
		}
	}
}

