using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
	public sealed partial class TypeDef
	{
		public static TypeDef Empty(TypeSignature signature) => new TypeDef(signature, null, ImmutableArray<SpecializedType>.Empty, ImmutableArray<MemberDef>.Empty);
	}
}
