using Coberec.CoreLib;
using System;
using System.Collections.Immutable;

namespace GeneratedProject.ModelNamespace
{
	public sealed class CustomValidatorTest : ITokenFormatable, ITraversableObject, IEquatable<CustomValidatorTest>
	{
		public string F1 {
			get;
		}

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("f1");

		int ITraversableObject.PropertyCount => 1;

		private CustomValidatorTest(NoNeedForValidationSentinel _, string f1)
		{
			F1 = f1;
		}

		public CustomValidatorTest(string f1)
			: this(default(NoNeedForValidationSentinel), f1)
		{
			ValidateObject(this).ThrowErrors("Could not initialize CustomValidatorTest due to validation errors", this);
		}

		private static ValidationErrors ValidateObject(CustomValidatorTest obj)
		{
			ValidationErrorsBuilder e = default(ValidationErrorsBuilder);
			e.Add(BasicValidators.NotNull(obj.F1).Nest("f1"));
			if (obj.F1 != null)
			{
				e.Add(Validators.MySpecialStringValidator(0, obj.F1).Nest("f1"));
			}
			return e.Build();
		}

		public static ValidationResult<CustomValidatorTest> Create(string f1)
		{
			CustomValidatorTest result = new CustomValidatorTest(default(NoNeedForValidationSentinel), f1);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create((object)"CustomValidatorTest {f1 = ", (object)F1, (object)"}"), new string[3]
			{
				"",
				"f1",
				""
			});
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return (propIndex == 0) ? F1 : null;
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

