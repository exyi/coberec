using Coberec.CoreLib;
using System;

namespace SampleProject.GeneratedSchema
{
	public sealed class TypeA : IEquatable<TypeA>
	{
		public int F {
			get;
		}

		public TypeA(int f)
		{
			F = f;
		}

		public static ValidationResult<TypeA> Create(int f)
		{
			TypeA value = new TypeA(f);
			return ValidationResult.Create(value);
		}

		public override int GetHashCode()
		{
			return F;
		}

		public bool Equals(TypeA b)
		{
			return (object)this == b || F == b.F;
		}

		public static bool operator ==(TypeA a, TypeA b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(TypeA a, TypeA b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			TypeA b2;
			return (object)(b2 = (b as TypeA)) != null && Equals(b2);
		}
	}
}
