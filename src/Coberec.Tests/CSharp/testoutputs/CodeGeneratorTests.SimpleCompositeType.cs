using Coberec.CoreLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace GeneratedProject.ModelNamespace
{
	public sealed class Test123 : IEquatable<Test123>
	{
		public ImmutableArray<string> Field543 {
			get;
		}

		public int AbcSS {
			get;
		}

		public Test123(ImmutableArray<string> field543, int abcSS)
		{
			Field543 = field543;
			AbcSS = abcSS;
		}

		public Test123(IEnumerable<string> field543, int abcSS)
			: this(field543.ToImmutableArray(), abcSS)
		{
		}

		public static ValidationResult<Test123> Create(ImmutableArray<string> field543, int abcSS)
		{
			Test123 result = new Test123(field543, abcSS);
			return ValidationResult.Create(result);
		}

		public static ValidationResult<Test123> Create(IEnumerable<string> field543, int abcSS)
		{
			return Create(field543.ToImmutableArray(), abcSS);
		}

		public override int GetHashCode()
		{
			return (StructuralComparisons.StructuralEqualityComparer.GetHashCode(Field543), AbcSS).GetHashCode();
		}

		public bool Equals(Test123 b)
		{
			return (object)this == b || ((object)b != null && StructuralComparisons.StructuralEqualityComparer.Equals(Field543, b.Field543) && AbcSS == b.AbcSS);
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

		public ValidationResult<Test123> With(ImmutableArray<string> field543, int abcSS)
		{
			return (StructuralComparisons.StructuralEqualityComparer.Equals(Field543, field543) && AbcSS == abcSS) ? ValidationResult.Create(this) : Create(field543, abcSS);
		}

		public ValidationResult<Test123> With(OptParam<ImmutableArray<string>> field543 = default(OptParam<ImmutableArray<string>>), OptParam<int> abcSS = default(OptParam<int>))
		{
			return With(field543.ValueOrDefault(Field543), abcSS.ValueOrDefault(AbcSS));
		}
	}
}

