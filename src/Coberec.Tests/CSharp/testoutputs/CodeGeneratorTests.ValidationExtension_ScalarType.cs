using System;
using Coberec.CoreLib;

namespace GeneratedProject.ModelNamespace
{
	public sealed partial class Scalar123 : IEquatable<Scalar123>
	{
		public string Value { get; }

		private Scalar123(NoNeedForValidationSentinel _, string value)
		{
			Value = value;
		}

		public Scalar123(string value)
			: this(default(NoNeedForValidationSentinel), value)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Scalar123 due to validation errors", this);
		}

		private static ValidationErrors ValidateObject(Scalar123 obj)
		{
			ValidationErrorsBuilder e = default(ValidationErrorsBuilder);
			e.Add(BasicValidators.NotNull(obj.Value).Nest("value"));
			ValidateObjectExtension(ref e, obj);
			return e.Build();
		}

		public static ValidationResult<Scalar123> Create(string value)
		{
			Scalar123 result = new Scalar123(default(NoNeedForValidationSentinel), value);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		static partial void ValidateObjectExtension(ref ValidationErrorsBuilder e, Scalar123 obj);

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

