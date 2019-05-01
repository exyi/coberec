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

		private static ValidationErrors ValidateObject(MyType obj)
		{
			return ValidationErrors.Join(BasicValidators.NotNull(obj.F3).Nest("f3"), BasicValidators.NotNull(obj.F4).Nest("f4"), Validators.CustomValidator(obj), (obj.F4 is object) ? Validators.CustomValidator(obj.F4).Nest("f4") : null, (obj.F1 is object) ? Validators.CustomValidator(obj.F1).Nest("f1") : null, (obj.F2 is object) ? Validators.MySpecialStringValidator(0, obj.F2).Nest("f2") : null, (obj.F3 is object) ? Validators.MySpecialStringValidator(12, obj.F3).Nest("f3") : null);
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

		public static ValidationResult<MyType> Create(MyType f1, string f2, string f3, MyType f4)
		{
			MyType myType = new MyType(default(NoNeedForValidationSentinel), f1, f2, f3, f4);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(myType), myType);
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
			return (F1 == f1 && F2 == f2 && F3 == f3 && F4 == f4) ? ValidationResult.Create(this) : Create(F1, F2, F3, F4);
		}

		public ValidationResult<MyType> With(OptParam<MyType> f1 = default(OptParam<MyType>), OptParam<string> f2 = default(OptParam<string>), OptParam<string> f3 = default(OptParam<string>), OptParam<MyType> f4 = default(OptParam<MyType>))
		{
			return With(f1.ValueOrDefault(F1), f2.ValueOrDefault(F2), f3.ValueOrDefault(F3), f4.ValueOrDefault(F4));
		}
	}
}

