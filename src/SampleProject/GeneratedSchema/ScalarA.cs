using Coberec.CoreLib;
using System;

namespace SampleProject.GeneratedSchema
{
	public sealed class ScalarA : IEquatable<ScalarA>
	{
		public string Value {
			get;
		}

		private static ValidationErrors ValidateObject(ScalarA obj)
		{
			return BasicValidators.NotNull(obj.Value).Nest("value");
		}

		private ScalarA(NoNeedForValidationSentinel _, string value)
		{
			Value = value;
		}

		public ScalarA(string value)
			: this(default(NoNeedForValidationSentinel), value)
		{
			ValidateObject(this).ThrowErrors("Could not initialize ScalarA due to validation errors");
		}

		public static ValidationResult<ScalarA> Create(string value)
		{
			ScalarA scalarA = new ScalarA(default(NoNeedForValidationSentinel), value);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(scalarA), scalarA);
		}

		public override int GetHashCode()
		{
			return Value.GetHashCode();
		}

		public bool Equals(ScalarA b)
		{
			return (object)this == b || Value == b.Value;
		}

		public static bool operator ==(ScalarA a, ScalarA b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(ScalarA a, ScalarA b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			ScalarA b2;
			return (object)(b2 = (b as ScalarA)) != null && Equals(b2);
		}
	}
}
