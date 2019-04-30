using Coberec.CoreLib;
using System;
using System.Collections;
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

		private static ValidationErrors ValidateObject(Test123 obj)
		{
			return ValidationErrors.Join(BasicValidators.NotEmpty(obj.Field543).Nest("Field543"), BasicValidators.Range(1, 10, obj.AbcSS).Nest("abcSS"));
		}

		private Test123(NoNeedForValidationSentinel _, ImmutableArray<string> field543, int abcSS)
		{
			Field543 = field543;
			AbcSS = abcSS;
		}

		public Test123(ImmutableArray<string> field543, int abcSS)
			: this(default(NoNeedForValidationSentinel), field543, abcSS)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Test123 due to validation errors");
		}

		public static ValidationResult<Test123> Create(ImmutableArray<string> field543, int abcSS)
		{
			Test123 test = new Test123(default(NoNeedForValidationSentinel), field543, abcSS);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(test), test);
		}

		public override int GetHashCode()
		{
			return (StructuralComparisons.StructuralEqualityComparer.GetHashCode(Field543), AbcSS).GetHashCode();
		}

		public bool Equals(Test123 b)
		{
			return (object)this == b || (StructuralComparisons.StructuralEqualityComparer.Equals(Field543, b.Field543) && AbcSS == b.AbcSS);
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

		public ValidationResult<Test123> With(ImmutableArray<string> field543, int abcSS)
		{
			return (StructuralComparisons.StructuralEqualityComparer.Equals(Field543, field543) && AbcSS == abcSS) ? ValidationResult.Create(this) : Create(Field543, AbcSS);
		}

		public ValidationResult<Test123> With(OptParam<ImmutableArray<string>> field543 = default(OptParam<ImmutableArray<string>>), OptParam<int> abcSS = default(OptParam<int>))
		{
			return With(field543.ValueOrDefault(Field543), abcSS.ValueOrDefault(AbcSS));
		}
	}
	public abstract class Union123 : IEquatable<Union123>
	{
		public sealed class Test123Case : Union123
		{
			public Test123 Item {
				get;
			}

			public Test123Case(Test123 item)
			{
				Item = item;
			}

			public override int GetHashCode()
			{
				return ((object)Item).GetHashCode();
			}

			private protected override bool EqualsCore(Union123 b)
			{
				Test123Case test123Case;
				return (test123Case = (b as Test123Case)) != null && Item == ((Test123Case)b).Item;
			}

			public override TResult Match<TResult>(Func<Test123Case, TResult> test123, Func<StringCase, TResult> @string)
			{
				return test123(this);
			}
		}

		public sealed class StringCase : Union123
		{
			public string Item {
				get;
			}

			public StringCase(string item)
			{
				Item = item;
			}

			public override int GetHashCode()
			{
				return Item.GetHashCode();
			}

			private protected override bool EqualsCore(Union123 b)
			{
				StringCase stringCase;
				return (stringCase = (b as StringCase)) != null && Item == ((StringCase)b).Item;
			}

			public override TResult Match<TResult>(Func<Test123Case, TResult> test123, Func<StringCase, TResult> @string)
			{
				return @string(this);
			}
		}

		private protected abstract bool EqualsCore(Union123 b);

		public virtual bool Equals(Union123 b)
		{
			return (object)this == b || EqualsCore(b);
		}

		public static bool operator ==(Union123 a, Union123 b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(Union123 a, Union123 b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			Union123 b2;
			return (object)(b2 = (b as Union123)) != null && Equals(b2);
		}

		public abstract TResult Match<TResult>(Func<Test123Case, TResult> test123, Func<StringCase, TResult> @string);

		public static Union123 Test123(Test123 item)
		{
			return new Test123Case(item);
		}

		public static Union123 String(string item)
		{
			return new StringCase(item);
		}

		public static implicit operator Union123(Test123 item)
		{
			return new Test123Case(item);
		}

		public static implicit operator Union123(string item)
		{
			return new StringCase(item);
		}
	}
}

