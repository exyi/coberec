using Coberec.CoreLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace GeneratedProject.ModelNamespace
{
	public sealed class Test123 : ITokenFormatable, ITraversableObject, IEquatable<Test123>
	{
		public ImmutableArray<string> Field543 {
			get;
		}

		public int AbcSS {
			get;
		}

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("Field543", "abcSS");

		int ITraversableObject.PropertyCount => 2;

		private Test123(NoNeedForValidationSentinel _, ImmutableArray<string> field543, int abcSS)
		{
			Field543 = field543;
			AbcSS = abcSS;
		}

		public Test123(ImmutableArray<string> field543, int abcSS)
			: this(default(NoNeedForValidationSentinel), field543, abcSS)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Test123 due to validation errors", this);
		}

		public Test123(IEnumerable<string> field543, int abcSS)
			: this(field543.ToImmutableArray(), abcSS)
		{
		}

		private static ValidationErrors ValidateObject(Test123 obj)
		{
			ValidationErrorsBuilder e = default(ValidationErrorsBuilder);
			e.Add(BasicValidators.NotEmpty(obj.Field543).Nest("Field543"));
			e.Add(BasicValidators.Range(1, 10, obj.AbcSS).Nest("abcSS"));
			return e.Build();
		}

		public static ValidationResult<Test123> Create(ImmutableArray<string> field543, int abcSS)
		{
			Test123 result = new Test123(default(NoNeedForValidationSentinel), field543, abcSS);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public static ValidationResult<Test123> Create(IEnumerable<string> field543, int abcSS)
		{
			return Create(field543.ToImmutableArray(), abcSS);
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create(new object[5]
			{
				"Test123 {Field543 = ",
				FmtToken.FormatArray(Field543),
				", abcSS = ",
				(object)(object)AbcSS,
				"}"
			}), new string[5]
			{
				"",
				"Field543",
				"",
				"abcSS",
				""
			});
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return (propIndex == 0) ? Field543 : ((propIndex == 1) ? ((object)(object)AbcSS) : null);
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
	public abstract class Union123 : IEquatable<Union123>
	{
		public sealed class Test123Case : Union123
		{
			public Test123 Item {
				get;
			}

			public override string ToString()
			{
				return Item.ToString();
			}

			public Test123Case(Test123 item)
			{
				Item = item;
			}

			public override T Match<T>(Func<Test123, T> test123, Func<string, T> @string)
			{
				return test123(Item);
			}

			public override int GetHashCode()
			{
				return Item.GetHashCode();
			}

			private protected override bool EqualsCore(Union123 b)
			{
				Test123Case test123Case;
				return (object)(test123Case = (b as Test123Case)) != null && Item == ((Test123Case)b).Item;
			}
		}

		public sealed class StringCase : Union123
		{
			public string Item {
				get;
			}

			public override string ToString()
			{
				return Item.ToString();
			}

			public StringCase(string item)
			{
				Item = item;
			}

			public override T Match<T>(Func<Test123, T> test123, Func<string, T> @string)
			{
				return @string(Item);
			}

			public override int GetHashCode()
			{
				return Item.GetHashCode();
			}

			private protected override bool EqualsCore(Union123 b)
			{
				StringCase stringCase;
				return (object)(stringCase = (b as StringCase)) != null && Item == ((StringCase)b).Item;
			}
		}

		public abstract T Match<T>(Func<Test123, T> test123, Func<string, T> @string);

		public abstract override int GetHashCode();

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
			return Equals(b as Union123);
		}

		public static Union123 Test123(Test123 item)
		{
			return new Test123Case(item);
		}

		public static Union123 Test123(ImmutableArray<string> field543, int abcSS)
		{
			return new Test123Case(new Test123(field543, abcSS));
		}

		public static Union123 Test123(IEnumerable<string> field543, int abcSS)
		{
			return new Test123Case(new Test123(field543, abcSS));
		}

		public static Union123 String(string item)
		{
			return new StringCase(item);
		}

		public static implicit operator Union123(Test123 item)
		{
			return ((object)item != null) ? new Test123Case(item) : null;
		}

		public static implicit operator Union123(string item)
		{
			return (item != null) ? new StringCase(item) : null;
		}
	}
}

