using System;
using System.Collections.Immutable;
using Coberec.CoreLib;

namespace GeneratedProject.ModelNamespace
{
	/// <summary> comment for interface Ifc </summary>
	public interface Ifc
	{
		string PropA { get; }

		ValidationResult<Ifc> With(string propA);
	}
	/// <summary> comment for type A </summary>
	public sealed class A : Ifc, ITokenFormatable, ITraversableObject, IEquatable<A>
	{
		/// <summary> comment for propA </summary>
		public string PropA { get; }

		/// <summary> comment for propAX </summary>
		public string PropAX { get; }

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("propA", "propAX");

		int ITraversableObject.PropertyCount => 2;

		private A(NoNeedForValidationSentinel _, string propA, string propAX)
		{
			PropA = propA;
			PropAX = propAX;
		}

		/// <summary> Creates new instance of A: comment for type A </summary>
		/// <param name="propA">comment for propA</param>
		/// <param name="propAX">comment for propAX</param>
		public A(string propA, string propAX)
			: this(default(NoNeedForValidationSentinel), propA, propAX)
		{
			ValidateObject(this).ThrowErrors("Could not initialize A due to validation errors", this);
		}

		private static ValidationErrors ValidateObject(A obj)
		{
			ValidationErrorsBuilder e = default(ValidationErrorsBuilder);
			e.Add(BasicValidators.NotNull(obj.PropA).Nest("propA"));
			e.Add(BasicValidators.NotNull(obj.PropAX).Nest("propAX"));
			return e.Build();
		}

		/// <summary> Creates new instance of A: comment for type A </summary>
		/// <param name="propA">comment for propA</param>
		/// <param name="propAX">comment for propAX</param>
		public static ValidationResult<A> Create(string propA, string propAX)
		{
			A result = new A(default(NoNeedForValidationSentinel), propA, propAX);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create(new object[5] { "A {propA = ", PropA, ", propAX = ", PropAX, "}" }), new string[5] { "", "propA", "", "propAX", "" });
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return (propIndex == 0) ? PropA : ((propIndex == 1) ? PropAX : null);
		}

		public override int GetHashCode()
		{
			return (PropA, PropAX).GetHashCode();
		}

		public bool Equals(A b)
		{
			return (object)this == b || ((object)b != null && PropA == b.PropA && PropAX == b.PropAX);
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

		/// <summary> Sets the specified properties while cloning the A: comment for type A </summary>
		/// <param name="propA">comment for propA</param>
		/// <param name="propAX">comment for propAX</param>
		public ValidationResult<A> With(string propA, string propAX)
		{
			return (PropA == propA && PropAX == propAX) ? ValidationResult.Create(this) : Create(propA, propAX);
		}

		/// <summary> Sets the specified properties while cloning the A: comment for type A </summary>
		/// <param name="propA">comment for propA</param>
		/// <param name="propAX">comment for propAX</param>
		public ValidationResult<A> With(OptParam<string> propA = default(OptParam<string>), OptParam<string> propAX = default(OptParam<string>))
		{
			return With(propA.ValueOrDefault(PropA), propAX.ValueOrDefault(PropAX));
		}

		ValidationResult<Ifc> Ifc.With(string propA)
		{
			return With(propA, PropAX).Cast<Ifc>();
		}
	}
	/// <summary> comment for type B </summary>
	public sealed class B : ITokenFormatable, ITraversableObject, IEquatable<B>
	{
		/// <summary> comment for propB </summary>
		public string PropB { get; }

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create("propB");

		int ITraversableObject.PropertyCount => 1;

		private B(NoNeedForValidationSentinel _, string propB)
		{
			PropB = propB;
		}

		/// <summary> Creates new instance of B: comment for type B </summary>
		/// <param name="propB">comment for propB</param>
		public B(string propB)
			: this(default(NoNeedForValidationSentinel), propB)
		{
			ValidateObject(this).ThrowErrors("Could not initialize B due to validation errors", this);
		}

		private static ValidationErrors ValidateObject(B obj)
		{
			return BasicValidators.NotNull(obj.PropB).Nest("propB");
		}

		/// <summary> Creates new instance of B: comment for type B </summary>
		/// <param name="propB">comment for propB</param>
		public static ValidationResult<B> Create(string propB)
		{
			B result = new B(default(NoNeedForValidationSentinel), propB);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(result), result);
		}

		public override string ToString()
		{
			return Format().ToString();
		}

		public FmtToken Format()
		{
			return FmtToken.Concat(ImmutableArray.Create((object)"B {propB = ", (object)PropB, (object)"}"), new string[3] { "", "propB", "" });
		}

		object ITraversableObject.GetValue(int propIndex)
		{
			return (propIndex == 0) ? PropB : null;
		}

		public override int GetHashCode()
		{
			return PropB.GetHashCode();
		}

		public bool Equals(B b)
		{
			return (object)this == b || ((object)b != null && PropB == b.PropB);
		}

		public static bool operator ==(B a, B b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(B a, B b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as B);
		}

		/// <summary> Sets the specified properties while cloning the B: comment for type B </summary>
		/// <param name="propB">comment for propB</param>
		public ValidationResult<B> With(string propB)
		{
			return (PropB == propB) ? ValidationResult.Create(this) : Create(propB);
		}
	}
	/// <summary> comment for union U </summary>
	public abstract class U : ITraversableObject, IEquatable<U>
	{
		/// <summary> comment for type A </summary>
		public sealed class ACase : U
		{
			public A Item { get; }

			public sealed override string CaseName => "A";

			public sealed override object RawItem => Item;

			public ACase(A item)
			{
				Item = item;
			}

			public override string ToString()
			{
				return Item.ToString();
			}

			public override T Match<T>(Func<A, T> a, Func<B, T> b)
			{
				return a(Item);
			}

			public override int GetHashCode()
			{
				return Item.GetHashCode();
			}

			private protected override bool EqualsCore(U b)
			{
				ACase aCase;
				return (object)(aCase = b as ACase) != null && Item == ((ACase)b).Item;
			}
		}

		/// <summary> comment for type B </summary>
		public sealed class BCase : U
		{
			public B Item { get; }

			public sealed override string CaseName => "B";

			public sealed override object RawItem => Item;

			public BCase(B item)
			{
				Item = item;
			}

			public override string ToString()
			{
				return Item.ToString();
			}

			public override T Match<T>(Func<A, T> a, Func<B, T> b)
			{
				return b(Item);
			}

			public override int GetHashCode()
			{
				return Item.GetHashCode();
			}

			private protected override bool EqualsCore(U b)
			{
				BCase bCase;
				return (object)(bCase = b as BCase) != null && Item == ((BCase)b).Item;
			}
		}

		public abstract string CaseName { get; }

		public abstract object RawItem { get; }

		int ITraversableObject.PropertyCount => 1;

		ImmutableArray<string> ITraversableObject.Properties => ImmutableArray.Create(CaseName);

		public abstract T Match<T>(Func<A, T> a, Func<B, T> b);

		public abstract override int GetHashCode();

		object ITraversableObject.GetValue(int propIndex)
		{
			return RawItem;
		}

		private protected abstract bool EqualsCore(U b);

		public virtual bool Equals(U b)
		{
			return (object)this == b || EqualsCore(b);
		}

		public static bool operator ==(U a, U b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(U a, U b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			return Equals(b as U);
		}

		/// <summary> comment for type A </summary>
		public static U A(A item)
		{
			return new ACase(item);
		}

		/// <summary> Creates new instance of A: comment for type A </summary>
		/// <param name="propA">comment for propA</param>
		/// <param name="propAX">comment for propAX</param>
		public static U A(string propA, string propAX)
		{
			return new ACase(new A(propA, propAX));
		}

		/// <summary> comment for type B </summary>
		public static U B(B item)
		{
			return new BCase(item);
		}

		/// <summary> Creates new instance of B: comment for type B </summary>
		/// <param name="propB">comment for propB</param>
		public static U B(string propB)
		{
			return new BCase(new B(propB));
		}

		public static implicit operator U(A item)
		{
			return ((object)item != null) ? new ACase(item) : null;
		}

		public static implicit operator U(B item)
		{
			return ((object)item != null) ? new BCase(item) : null;
		}
	}
}

