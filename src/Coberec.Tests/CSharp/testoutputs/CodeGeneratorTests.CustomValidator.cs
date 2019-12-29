using Coberec.CoreLib;
using System;

namespace GeneratedProject.ModelNamespace
{
	public sealed class CustomValidatorTest : IEquatable<CustomValidatorTest>
	{
		public string F1 {
			get;
		}

		private CustomValidatorTest(NoNeedForValidationSentinel _, string f1)
		{
			F1 = f1;
		}

		public CustomValidatorTest(string f1)
			: this(default(NoNeedForValidationSentinel), f1)
		{
			ValidateObject(this).ThrowErrors("Could not initialize CustomValidatorTest due to validation errors");
		}

		private static ValidationErrors ValidateObject(CustomValidatorTest obj)
		{
			ValidationErrors[] tmpArray = new ValidationErrors[2]
			{
				BasicValidators.NotNull(obj.F1).Nest("f1"),
				null
			};
			ref ValidationErrors reference = ref tmpArray[1];
			ValidationErrors validationErrors;
			if (obj.F1 != null)
			{
				validationErrors = Validators.MySpecialStringValidator(0, obj.F1).Nest("f1");
			}
			else
			{
				validationErrors = null;
			}
			reference = validationErrors;
			return ValidationErrors.Join(tmpArray);
		}

		public static ValidationResult<CustomValidatorTest> Create(string f1)
		{
			CustomValidatorTest result = new CustomValidatorTest(default(NoNeedForValidationSentinel), f1);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override int GetHashCode()
		{
			return F1.GetHashCode();
		}

		public bool Equals(CustomValidatorTest b)
		{
			return (object)this == b || ((object)b != null && F1 == b.F1);
		}

		public static bool operator ==(CustomValidatorTest a, CustomValidatorTest b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(CustomValidatorTest a, CustomValidatorTest b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as CustomValidatorTest);
		}

		public ValidationResult<CustomValidatorTest> With(string f1)
		{
			return (F1 == f1) ? ValidationResult.Create(this) : Create(f1);
		}
	}
}

