using Coberec.CoreLib;
using System;

namespace SampleProject.GeneratedSchema
{
	public sealed class TypeB : IEquatable<TypeB>
	{
		public ScalarA A {
			get;
		}

		public Uri PhotoUrl {
			get;
		}

		public int Num {
			get;
		}

		private static ValidationErrors ValidateObject(TypeB obj)
		{
			return ValidationErrors.Join(BasicValidators.NotNull(obj.A).Nest("a"), BasicValidators.NotNull(obj.PhotoUrl).Nest("photoUrl"), BasicValidators.Range(0, 100, obj.Num).Nest("num"));
		}

		private TypeB(NoNeedForValidationSentinel _, ScalarA a, Uri photoUrl, int num)
		{
			A = a;
			PhotoUrl = photoUrl;
			Num = num;
		}

		public TypeB(ScalarA a, Uri photoUrl, int num)
			: this(default(NoNeedForValidationSentinel), a, photoUrl, num)
		{
			ValidateObject(this).ThrowErrors("Could not initialize TypeB due to validation errors");
		}

		public static ValidationResult<TypeB> Create(ScalarA a, Uri photoUrl, int num)
		{
			TypeB typeB = new TypeB(default(NoNeedForValidationSentinel), a, photoUrl, num);
			return ValidationResult.CreateErrorsOrValue(ValidateObject(typeB), typeB);
		}

		public override int GetHashCode()
		{
			return (A, PhotoUrl, Num).GetHashCode();
		}

		public bool Equals(TypeB b)
		{
			return (object)this == b || (A == b.A && PhotoUrl == b.PhotoUrl && Num == b.Num);
		}

		public static bool operator ==(TypeB a, TypeB b)
		{
			return (object)a == b || (a?.Equals(b) ?? false);
		}

		public static bool operator !=(TypeB a, TypeB b)
		{
			return !(a == b);
		}

		public override bool Equals(object b)
		{
			TypeB b2;
			return (object)(b2 = (b as TypeB)) != null && Equals(b2);
		}
	}
}
