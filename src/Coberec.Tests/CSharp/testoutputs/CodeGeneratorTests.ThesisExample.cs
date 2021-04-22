using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Coberec.CoreLib;

namespace GeneratedProject.ModelNamespace
{
	public sealed class T : ITokenFormatable, ITraversableObject, IEquatable<T>
	{
		public int A { get; }

		public ImmutableArray<string> B { get; }

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("a", "b");

		int ITraversableObject.PropertyCount => 2;

		private T(NoNeedForValidationSentinel _, int a, ImmutableArray<string> b)
		{
			A = a;
			B = b;
		}

		public T(int a, ImmutableArray<string> b)
			: this(default(NoNeedForValidationSentinel), a, b)
		{
			ValidateObject(this).ThrowErrors("Could not initialize T due to validation errors", this);
		}

		public T(int a, IEnumerable<string> b)
			: this(a, b.ToImmutableArray())
		{
		}

		private static ValidationErrors ValidateObject(T obj)
		{
			return BasicValidators.NotEmpty(obj.B).Nest("b");
		}

		public static ValidationResult<T> Create(int a, ImmutableArray<string> b)
		{
			T result = new T(default(NoNeedForValidationSentinel), a, b);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public static ValidationResult<T> Create(int a, IEnumerable<string> b)
		{
			return Create(a, b.ToImmutableArray());
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create(new object[5]
			{
				"T {a = ",
				A,
				", b = ",
				FmtToken.FormatArray(B),
				"}"
			}), new string[5] { "", "a", "", "b", "" });
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return (propIndex == 0) ? ((object)A) : ((propIndex == 1) ? ((object)B) : null);
		}

		public override int GetHashCode()
		{
			return (A, StructuralComparisons.StructuralEqualityComparer.GetHashCode(B)).GetHashCode();
		}

		public bool Equals(T b)
		{
			return (object)this == b || ((object)b != null && A == b.A && StructuralComparisons.StructuralEqualityComparer.Equals(B, b.B));
		}

		public static bool operator ==(T a, T b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(T a, T b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as T);
		}

		public ValidationResult<T> With(int a, ImmutableArray<string> b)
		{
			return (A == a && StructuralComparisons.StructuralEqualityComparer.Equals(B, b)) ? ValidationResult.Create(this) : Create(a, b);
		}

		public ValidationResult<T> With(OptParam<int> a = default(OptParam<int>), OptParam<ImmutableArray<string>> b = default(OptParam<ImmutableArray<string>>))
		{
			return With(a.ValueOrDefault(A), b.ValueOrDefault(B));
		}
	}
}

