using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using Coberec.CoreLib;

namespace GeneratedProject.ModelNamespace
{
	public sealed class A : ITokenFormatable, ITraversableObject, IEquatable<A>
	{
		public ImmutableArray<string> H { get; }

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("h");

		int ITraversableObject.PropertyCount => 1;

		public A(ImmutableArray<string> h)
		{
			H = h;
		}

		public A(IEnumerable<string> h)
			: this(h.ToImmutableArray())
		{
		}

		public static ValidationResult<A> Create(ImmutableArray<string> h)
		{
			A result = new A(h);
			return ValidationResult.Create(result);
		}

		public static ValidationResult<A> Create(IEnumerable<string> h)
		{
			return Create(h.ToImmutableArray());
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create((object)"a {h = ", (object)FmtToken.FormatArray(H), (object)"}"), new string[3] { "", "h", "" });
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return (propIndex == 0) ? ((object)H) : null;
		}

		public override int GetHashCode()
		{
			return StructuralComparisons.StructuralEqualityComparer.GetHashCode(H);
		}

		public bool Equals(A b)
		{
			return (object)this == b || ((object)b != null && StructuralComparisons.StructuralEqualityComparer.Equals(H, b.H));
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

		public ValidationResult<A> With(ImmutableArray<string> h)
		{
			return StructuralComparisons.StructuralEqualityComparer.Equals(H, h) ? ValidationResult.Create(this) : Create(h);
		}
	}
}

