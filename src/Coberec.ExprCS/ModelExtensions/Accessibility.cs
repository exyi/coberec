using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
	/// <summary> Defines accessibility of a metadata member </summary>
	public sealed partial class Accessibility
	{
		/// <summary> `internal` </summary>
		public static Accessibility AInternal = new Accessibility(false, false, true);
		/// <summary> `public` </summary>
		public static Accessibility APublic = new Accessibility(true, true, true);
		/// <summary> `private` </summary>
		public static Accessibility APrivate = new Accessibility(false, false, false);
		/// <summary> `protected` </summary>
		public static Accessibility AProtected = new Accessibility(false, true, false);
		/// <summary> `protected internal` </summary>
		public static Accessibility AProtectedInternal = new Accessibility(false, true, true);
		/// <summary> `private protected` </summary>
		public static Accessibility APrivateProtected = new Accessibility(true, false, false);

		/// <summary> Returns the "most public" of the two accessibilities. </summary>
		public static Accessibility Max(Accessibility a, Accessibility b)
		{
			if (a == null) return b;
			if (b == null) return a;

			if (a == b) return a;
			if (a == APublic || b == APublic) return APublic;
			if (a == AProtectedInternal || b == AProtectedInternal) return AProtectedInternal;

			if (a == APrivate) return b;
			if (b == APrivate) return a;
			if (a == APrivateProtected) return b;
			if (b == APrivateProtected) return a;

			// only remaining options are `internal` and `protected` and we certainly have both of them
			return AProtectedInternal;
		}

		public override string ToString() =>
			this == AInternal ? "internal" :
			this == APublic ? "public" :
			this == APrivate ? "private" :
			this == AProtected ? "protected" :
			this == AProtectedInternal ? "protected internal" :
			this == APrivateProtected ? "private protected" :
			throw new NotSupportedException();
	}
}
