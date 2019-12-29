using Coberec.CoreLib;
using System;

namespace GeneratedProject.ModelNamespace
{
	public sealed class MyType : IEquatable<MyType>
	{
		public MyType F1 {
			get;
		}

		public string F2 {
			get;
		}

		public string F3 {
			get;
		}

		public MyType F4 {
			get;
		}

		private MyType(NoNeedForValidationSentinel _, MyType f1, string f2, string f3, MyType f4)
		{
			F1 = f1;
			F2 = f2;
			F3 = f3;
			F4 = f4;
		}

		public MyType(MyType f1, string f2, string f3, MyType f4)
			: this(default(NoNeedForValidationSentinel), f1, f2, f3, f4)
		{
			ValidateObject(this).ThrowErrors("Could not initialize MyType due to validation errors");
		}

		private static ValidationErrors ValidateObject(MyType obj)
		{
			ValidationErrors[] tmpArray = new ValidationErrors[7]
			{
				BasicValidators.NotNull(obj.F3).Nest("f3"),
				BasicValidators.NotNull(obj.F4).Nest("f4"),
				Validators.CustomValidator(obj),
				null,
				null,
				null,
				null
			};
			ref ValidationErrors reference = ref tmpArray[3];
			ValidationErrors validationErrors;
			if ((object)obj.F4 != null)
			{
				validationErrors = Validators.CustomValidator(obj.F4).Nest("f4");
			}
			else
			{
				validationErrors = null;
			}
			reference = validationErrors;
			ref ValidationErrors reference2 = ref tmpArray[4];
			ValidationErrors validationErrors2;
			if ((object)obj.F1 != null)
			{
				validationErrors2 = Validators.CustomValidator(obj.F1).Nest("f1");
			}
			else
			{
				validationErrors2 = null;
			}
			reference2 = validationErrors2;
			ref ValidationErrors reference3 = ref tmpArray[5];
			ValidationErrors validationErrors3;
			if (obj.F2 != null)
			{
				validationErrors3 = Validators.MySpecialStringValidator(0, obj.F2).Nest("f2");
			}
			else
			{
				validationErrors3 = null;
			}
			reference3 = validationErrors3;
			ref ValidationErrors reference4 = ref tmpArray[6];
			ValidationErrors validationErrors4;
			if (obj.F3 != null)
			{
				validationErrors4 = Validators.MySpecialStringValidator(12, obj.F3).Nest("f3");
			}
			else
			{
				validationErrors4 = null;
			}
			reference4 = validationErrors4;
			return ValidationErrors.Join(tmpArray);
		}

		public static ValidationResult<MyType> Create(MyType f1, string f2, string f3, MyType f4)
		{
			MyType result = new MyType(default(NoNeedForValidationSentinel), f1, f2, f3, f4);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override int GetHashCode()
		{
			return (F1, F2, F3, F4).GetHashCode();
		}

		public bool Equals(MyType b)
		{
			return (object)this == b || ((object)b != null && F1 == b.F1 && F2 == b.F2 && F3 == b.F3 && F4 == b.F4);
		}

		public static bool operator ==(MyType a, MyType b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(MyType a, MyType b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as MyType);
		}

		public ValidationResult<MyType> With(MyType f1, string f2, string f3, MyType f4)
		{
			return (F1 == f1 && F2 == f2 && F3 == f3 && F4 == f4) ? ValidationResult.Create(this) : Create(f1, f2, f3, f4);
		}

		public ValidationResult<MyType> With(OptParam<MyType> f1 = default(OptParam<MyType>), OptParam<string> f2 = default(OptParam<string>), OptParam<string> f3 = default(OptParam<string>), OptParam<MyType> f4 = default(OptParam<MyType>))
		{
			return With(f1.ValueOrDefault(F1), f2.ValueOrDefault(F2), f3.ValueOrDefault(F3), f4.ValueOrDefault(F4));
		}
	}
}

