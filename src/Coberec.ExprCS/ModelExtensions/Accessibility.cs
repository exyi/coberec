using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
	public sealed partial class Accessibility : IEquatable<Accessibility>
	{
		public static Accessibility AInternal = new Accessibility(false, false, true);
		public static Accessibility APublic = new Accessibility(true, true, true);
		public static Accessibility APrivate = new Accessibility(false, false, false);
		public static Accessibility AProtected = new Accessibility(false, true, false);
		public static Accessibility AProtectedInternal = new Accessibility(false, true, true);
		public static Accessibility APrivateProtected = new Accessibility(true, false, false);
	}
}
