using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
	public sealed partial class TypeDef
	{
		public static TypeDef Empty(TypeSignature signature) => new TypeDef(signature, null, ImmutableArray<SpecializedType>.Empty, ImmutableArray<MemberDef>.Empty);

		public TypeDef AddMember(params MemberDef[] members) =>
			this.With(
				members: this.Members.AddRange(members));
		public TypeDef AddImplements(params SpecializedType[] interfaces) =>
			this.With(
				implements: this.Implements.AddRange(interfaces));
	}
}
