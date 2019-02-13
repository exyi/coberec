using System;

namespace SampleProject.GeneratedSchema
{
	public abstract class UnionAB : IEquatable<UnionAB>
	{
		public sealed class TypeACase : UnionAB
		{
			public TypeA Item {
				get;
			}

			public TypeACase(TypeA item)
			{
				Item = item;
			}

			public override int GetHashCode()
			{
				return ((object)Item).GetHashCode();
			}

			private protected override bool EqualsCore(UnionAB b)
			{
				TypeACase typeACase;
				return (typeACase = (b as TypeACase)) != null && Item == ((TypeACase)b).Item;
			}

			public override TResult Match<TResult>(Func<TypeACase, TResult> typeA, Func<TypeBCase, TResult> typeB)
			{
				return typeA(this);
			}
		}

		public sealed class TypeBCase : UnionAB
		{
			public TypeB Item {
				get;
			}

			public TypeBCase(TypeB item)
			{
				Item = item;
			}

			public override int GetHashCode()
			{
				return ((object)Item).GetHashCode();
			}

			private protected override bool EqualsCore(UnionAB b)
			{
				TypeBCase typeBCase;
				return (typeBCase = (b as TypeBCase)) != null && Item == ((TypeBCase)b).Item;
			}

			public override TResult Match<TResult>(Func<TypeACase, TResult> typeA, Func<TypeBCase, TResult> typeB)
			{
				return typeB(this);
			}
		}

		private protected abstract bool EqualsCore(UnionAB b);

		public virtual bool Equals(UnionAB b)
		{
			return (object)this == b || EqualsCore(b);
		}

		public static bool operator ==(UnionAB a, UnionAB b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(UnionAB a, UnionAB b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			UnionAB b2;
			return (object)(b2 = (b as UnionAB)) != null && Equals(b2);
		}

		public abstract TResult Match<TResult>(Func<TypeACase, TResult> typeA, Func<TypeBCase, TResult> typeB);

		public static UnionAB TypeA(TypeA item)
		{
			return new TypeACase(item);
		}

		public static UnionAB TypeB(TypeB item)
		{
			return new TypeBCase(item);
		}
	}
}
