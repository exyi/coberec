using Coberec.CoreLib;
using System;
using System.Collections.Immutable;

namespace GeneratedProject.ModelNamespace
{
	public sealed class A : ITokenFormatable, ITraversableObject, IEquatable<A>
	{
		public string H {
			get;
		}

		public int H2 {
			get;
		}

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("h", "H");

		int ITraversableObject.PropertyCount => 2;

		private A(NoNeedForValidationSentinel _, string h, int h2)
		{
			H = h;
			H2 = h2;
		}

		public A(string h, int h2)
			: this(default(NoNeedForValidationSentinel), h, h2)
		{
			ValidateObject(this).ThrowErrors("Could not initialize a due to validation errors", this);
		}

		private static ValidationErrors ValidateObject(A obj)
		{
			return BasicValidators.NotNull(obj.H).Nest("h");
		}

		public static ValidationResult<A> Create(string h, int h2)
		{
			A result = new A(default(NoNeedForValidationSentinel), h, h2);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create(new object[5]
			{
				"a {h = ",
				H,
				", H = ",
				H2,
				"}"
			}), new string[5]
			{
				"",
				"h",
				"",
				"H",
				""
			});
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return (propIndex == 0) ? H : ((propIndex == 1) ? ((object)H2) : null);
		}

		public override int GetHashCode()
		{
			return (H, H2).GetHashCode();
		}

		public bool Equals(A b)
		{
			return (object)this == b || ((object)b != null && H == b.H && H2 == b.H2);
		}

		public static bool operator ==(A a, A b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(A a, A b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as A);
		}

		public ValidationResult<A> With(string h, int h2)
		{
			return (H == h && H2 == h2) ? ValidationResult.Create(this) : Create(h, h2);
		}

		public ValidationResult<A> With(OptParam<string> h = default(OptParam<string>), OptParam<int> h2 = default(OptParam<int>))
		{
			return With(h.ValueOrDefault(H), h2.ValueOrDefault(H2));
		}
	}
}

