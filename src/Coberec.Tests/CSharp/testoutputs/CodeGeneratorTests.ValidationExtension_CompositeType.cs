using Coberec.CoreLib;
using System;
using System.Collections.Immutable;

namespace GeneratedProject.ModelNamespace
{
	public sealed partial class Composite123 : ITokenFormatable, ITraversableObject, IEquatable<Composite123>
	{
		public string Field543 {
			get;
		}

		public int AbcSS {
			get;
		}

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("Field543", "abcSS");

		int ITraversableObject.PropertyCount => 2;

		private Composite123(NoNeedForValidationSentinel _, string field543, int abcSS)
		{
			Field543 = field543;
			AbcSS = abcSS;
		}

		public Composite123(string field543, int abcSS)
			: this(default(NoNeedForValidationSentinel), field543, abcSS)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Composite123 due to validation errors", this);
		}

		private static ValidationErrors ValidateObject(Composite123 obj)
		{
			ValidationErrorsBuilder e = default(ValidationErrorsBuilder);
			e.Add(BasicValidators.NotNull(obj.Field543).Nest("Field543"));
			if (obj.Field543 != null)
			{
				e.Add(Validators.MySpecialStringValidator(0, obj.Field543).Nest("Field543"));
			}
			e.Add(BasicValidators.Range(1, 10, obj.AbcSS).Nest("abcSS"));
			ValidateObjectExtension(ref e, obj);
			return e.Build();
		}

		public static ValidationResult<Composite123> Create(string field543, int abcSS)
		{
			Composite123 result = new Composite123(default(NoNeedForValidationSentinel), field543, abcSS);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		static partial void ValidateObjectExtension(ref ValidationErrorsBuilder e, Composite123 obj);

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create(new object[5]
			{
				"Composite123 {Field543 = ",
				Field543,
				", abcSS = ",
				AbcSS,
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
			return (propIndex == 0) ? Field543 : ((propIndex == 1) ? ((object)AbcSS) : null);
		}

		public override int GetHashCode()
		{
			return (Field543, AbcSS).GetHashCode();
		}

		public bool Equals(Composite123 b)
		{
			return (object)this == b || ((object)b != null && Field543 == b.Field543 && AbcSS == b.AbcSS);
		}

		public static bool operator ==(Composite123 a, Composite123 b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(Composite123 a, Composite123 b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as Composite123);
		}

		public ValidationResult<Composite123> With(string field543, int abcSS)
		{
			return (Field543 == field543 && AbcSS == abcSS) ? ValidationResult.Create(this) : Create(field543, abcSS);
		}

		public ValidationResult<Composite123> With(OptParam<string> field543 = default(OptParam<string>), OptParam<int> abcSS = default(OptParam<int>))
		{
			return With(field543.ValueOrDefault(Field543), abcSS.ValueOrDefault(AbcSS));
		}
	}
}

