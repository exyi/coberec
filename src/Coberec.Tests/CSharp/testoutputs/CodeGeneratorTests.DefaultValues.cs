using Coberec.CoreLib;
using System;
using System.Collections;
using System.Collections.Immutable;

namespace GeneratedProject.ModelNamespace
{
	public sealed class Composite123 : IEquatable<Composite123>
	{
		public string StringF {
			get;
		}

		public string NullableStringF {
			get;
		}

		public int? NullableIntF {
			get;
		}

		public int? IntF {
			get;
		}

		public double FloatF {
			get;
		}

		public ImmutableArray<string>? NullListF {
			get;
		}

		public Composite123 ThisF {
			get;
		}

		private Composite123(NoNeedForValidationSentinel _, string stringF, string nullableStringF, int? nullableIntF, int? intF, double floatF, ImmutableArray<string>? nullListF, Composite123 thisF)
		{
			StringF = stringF;
			NullableStringF = nullableStringF;
			NullableIntF = nullableIntF;
			IntF = intF;
			FloatF = floatF;
			NullListF = nullListF;
			ThisF = thisF;
		}

		public Composite123(string stringF = "abcd", string nullableStringF = null, int? nullableIntF = null, int? intF = 12, double floatF = 12.12, ImmutableArray<string>? nullListF = null, Composite123 thisF = null)
			: this(default(NoNeedForValidationSentinel), stringF, nullableStringF, nullableIntF, intF, floatF, nullListF, thisF)
		{
			ValidateObject(this).ThrowErrors("Could not initialize Composite123 due to validation errors", this);
		}

		private static ValidationErrors ValidateObject(Composite123 obj)
		{
			return BasicValidators.NotNull(obj.StringF).Nest("StringF");
		}

		public static ValidationResult<Composite123> Create(string stringF, string nullableStringF, int? nullableIntF, int? intF, double floatF, ImmutableArray<string>? nullListF, Composite123 thisF)
		{
			Composite123 result = new Composite123(default(NoNeedForValidationSentinel), stringF, nullableStringF, nullableIntF, intF, floatF, nullListF, thisF);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override string ToString()
		{
			return string.Concat("Composite123 {StringF = ", StringF, ", NullableStringF = ", NullableStringF, ", NullableIntF = ", NullableIntF, ", IntF = ", IntF, ", FloatF = ", (object)(object)FloatF, ", NullListF = [", string.Join<string>(", ", NullListF), "], ThisF = ", ThisF, "}");
		}

		public override int GetHashCode()
		{
			return (StringF, NullableStringF, NullableIntF, IntF, FloatF, NullListF, ThisF).GetHashCode();
		}

		public bool Equals(Composite123 b)
		{
			return (object)this == b || ((object)b != null && StringF == b.StringF && NullableStringF == b.NullableStringF && NullableIntF == b.NullableIntF && IntF == b.IntF && FloatF == b.FloatF && StructuralComparisons.StructuralEqualityComparer.Equals(NullListF, b.NullListF) && ThisF == b.ThisF);
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

		public ValidationResult<Composite123> With(string stringF, string nullableStringF, int? nullableIntF, int? intF, double floatF, ImmutableArray<string>? nullListF, Composite123 thisF)
		{
			return (StringF == stringF && NullableStringF == nullableStringF && NullableIntF == nullableIntF && IntF == intF && FloatF == floatF && StructuralComparisons.StructuralEqualityComparer.Equals(NullListF, nullListF) && ThisF == thisF) ? ValidationResult.Create(this) : Create(stringF, nullableStringF, nullableIntF, intF, floatF, nullListF, thisF);
		}

		public ValidationResult<Composite123> With(OptParam<string> stringF = default(OptParam<string>), OptParam<string> nullableStringF = default(OptParam<string>), OptParam<int?> nullableIntF = default(OptParam<int?>), OptParam<int?> intF = default(OptParam<int?>), OptParam<double> floatF = default(OptParam<double>), OptParam<ImmutableArray<string>?> nullListF = default(OptParam<ImmutableArray<string>?>), OptParam<Composite123> thisF = default(OptParam<Composite123>))
		{
			return With(stringF.ValueOrDefault(StringF), nullableStringF.ValueOrDefault(NullableStringF), nullableIntF.ValueOrDefault(NullableIntF), intF.ValueOrDefault(IntF), floatF.ValueOrDefault(FloatF), nullListF.ValueOrDefault(NullListF), thisF.ValueOrDefault(ThisF));
		}
	}
}

