using Coberec.CoreLib;
using System;
using System.Collections;
using System.Collections.Generic;
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

		ValidationResult<Interface1> With(ImmutableArray<string> field543, ImmutableArray<int> someName);
	}
	public sealed class Test123 : Interface1, ITokenFormatable, ITraversableObject, IEquatable<Test123>
	{
		public ImmutableArray<string> Field543 {
			get;
		}

		public ImmutableArray<int> SomeName {
			get;
		}

		public int? AbcSS {
			get;
		}

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("Field543", "someName", "abcSS");

		int ITraversableObject.PropertyCount => 3;

		private Test123(NoNeedForValidationSentinel _, ImmutableArray<string> field543, ImmutableArray<int> someName, int? abcSS)
		{
			Field543 = field543;
			SomeName = someName;
			AbcSS = abcSS;
		}

		public Test123(ImmutableArray<string> field543, ImmutableArray<int> someName, int? abcSS)
			: this(default(NoNeedForValidationSentinel), field543, someName, abcSS)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Test123 due to validation errors", this);
		}

		public Test123(IEnumerable<string> field543, IEnumerable<int> someName, int? abcSS)
			: this(field543.ToImmutableArray(), someName.ToImmutableArray(), abcSS)
		{
		}

		private static ValidationErrors ValidateObject(Test123 obj)
		{
			ValidationErrorsBuilder e = default(ValidationErrorsBuilder);
			e.Add(BasicValidators.NotEmpty(obj.Field543).Nest("Field543"));
			if (obj.AbcSS.HasValue)
			{
				e.Add(BasicValidators.Range(1, 10, obj.AbcSS.Value).Nest("abcSS"));
			}
			return e.Build();
		}

		public static ValidationResult<Test123> Create(ImmutableArray<string> field543, ImmutableArray<int> someName, int? abcSS)
		{
			Test123 result = new Test123(default(NoNeedForValidationSentinel), field543, someName, abcSS);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public static ValidationResult<Test123> Create(IEnumerable<string> field543, IEnumerable<int> someName, int? abcSS)
		{
			return Create(field543.ToImmutableArray(), someName.ToImmutableArray(), abcSS);
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create(new object[7]
			{
				"Test123 {Field543 = ",
				FmtToken.FormatArray(Field543),
				", someName = ",
				FmtToken.FormatArray(SomeName),
				", abcSS = ",
				AbcSS,
				"}"
			}), new string[7]
			{
				"",
				"Field543",
				"",
				"someName",
				"",
				"abcSS",
				""
			});
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return (propIndex == 0) ? Field543 : ((propIndex == 1) ? ((ValueType)SomeName) : ((ValueType)((propIndex == 2) ? AbcSS : null)));
		}

		public override int GetHashCode()
		{
			return (StructuralComparisons.StructuralEqualityComparer.GetHashCode(Field543), StructuralComparisons.StructuralEqualityComparer.GetHashCode(SomeName), AbcSS).GetHashCode();
		}

		public bool Equals(Test123 b)
		{
			return (object)this == b || ((object)b != null && StructuralComparisons.StructuralEqualityComparer.Equals(Field543, b.Field543) && StructuralComparisons.StructuralEqualityComparer.Equals(SomeName, b.SomeName) && AbcSS == b.AbcSS);
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
			return Equals(b as Test123);
		}

		public ValidationResult<Test123> With(ImmutableArray<string> field543, ImmutableArray<int> someName, int? abcSS)
		{
			return (StructuralComparisons.StructuralEqualityComparer.Equals(Field543, field543) && StructuralComparisons.StructuralEqualityComparer.Equals(SomeName, someName) && AbcSS == abcSS) ? ValidationResult.Create(this) : Create(field543, someName, abcSS);
		}

		ValidationResult<Interface1> Interface1.With(ImmutableArray<string> field543, ImmutableArray<int> someName)
		{
			return With(field543, someName, AbcSS).Cast<Interface1>();
		}
	}
}

