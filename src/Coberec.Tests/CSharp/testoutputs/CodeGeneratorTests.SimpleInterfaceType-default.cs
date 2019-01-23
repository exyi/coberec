using Coberec.CoreLib;
using System;
using System.Collections;
using System.Collections.Immutable;

namespace GeneratedProject.ModelNamespace
{
	public interface Interface1
	{
		ImmutableArray<string> Field543 {
			get;
		}

		ImmutableArray<int> SomeName {
			get;
		}

		ValidationResult<Interface1> With(OptParam<ImmutableArray<string>> field543 = default(OptParam<ImmutableArray<string>>), OptParam<ImmutableArray<int>> someName = default(OptParam<ImmutableArray<int>>));
	}
	public sealed class Test123 : IEquatable<Test123>, Interface1
	{
		public ImmutableArray<string> Field543 {
			get;
		}

		public ImmutableArray<int> SomeName {
			get;
		}

		public int AbcSS {
			get;
		}

		private static ValidationErrors ValidateObject(Test123 obj)
		{
			return ValidationErrors.Join(BasicValidators.NotEmpty(obj.Field543), BasicValidators.Range(1, 10, obj.AbcSS));
		}

		private Test123(NoNeedForValidationSentinel _, ImmutableArray<string> field543, ImmutableArray<int> someName, int abcSS)
		{
			Field543 = field543;
			SomeName = someName;
			AbcSS = abcSS;
		}

		public Test123(ImmutableArray<string> field543, ImmutableArray<int> someName, int abcSS)
			: this(default(NoNeedForValidationSentinel), field543, someName, abcSS)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Test123 due to validation errors");
		}

		public static ValidationResult<Test123> Create(ImmutableArray<string> field543, ImmutableArray<int> someName, int abcSS)
		{
			Test123 test = new Test123(default(NoNeedForValidationSentinel), field543, someName, abcSS);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(test), test);
		}

		public override int GetHashCode()
		{
			return (StructuralComparisons.StructuralEqualityComparer.GetHashCode(Field543), StructuralComparisons.StructuralEqualityComparer.GetHashCode(SomeName), AbcSS).GetHashCode();
		}

		public bool Equals(Test123 b)
		{
			return (object)this == b || (StructuralComparisons.StructuralEqualityComparer.Equals(Field543, b.Field543) && StructuralComparisons.StructuralEqualityComparer.Equals(SomeName, b.SomeName) && AbcSS == b.AbcSS);
		}

		public static bool operator ==(Test123 a, Test123 b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(Test123 a, Test123 b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			Test123 b2;
			return (object)(b2 = (b as Test123)) != null && Equals(b2);
		}

		public ValidationResult<Test123> With(ImmutableArray<string> field543, ImmutableArray<int> someName, int abcSS)
		{
			return (StructuralComparisons.StructuralEqualityComparer.Equals(Field543, field543) && StructuralComparisons.StructuralEqualityComparer.Equals(SomeName, someName) && AbcSS == abcSS) ? ValidationResult.Create(this) : Create(Field543, SomeName, AbcSS);
		}

		public ValidationResult<Test123> With(OptParam<ImmutableArray<string>> field543 = default(OptParam<ImmutableArray<string>>), OptParam<ImmutableArray<int>> someName = default(OptParam<ImmutableArray<int>>), OptParam<int> abcSS = default(OptParam<int>))
		{
			return With(field543.ValueOrDefault(Field543), someName.ValueOrDefault(SomeName), abcSS.ValueOrDefault(AbcSS));
		}

		ValidationResult<Interface1> Interface1.With(OptParam<ImmutableArray<string>> field543 = default(OptParam<ImmutableArray<string>>), OptParam<ImmutableArray<int>> someName = default(OptParam<ImmutableArray<int>>))
		{
			return With(field543.ValueOrDefault(Field543), someName.ValueOrDefault(SomeName), AbcSS).Cast<Interface1>();
		}
	}
}

