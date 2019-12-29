using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
	/// <summary> Represents a definition of a type. Apart from the (<see cref="TypeDef.Signature" />) contains the type members (<see cref="TypeDef.Members" />), attributes (TODO) and list of interface implementations (<see cref="Implements" />). </summary>
	public sealed partial class TypeDef
	{
		public static TypeDef Empty(TypeSignature signature) => new TypeDef(signature, null, ImmutableArray<SpecializedType>.Empty, ImmutableArray<MemberDef>.Empty);

		public TypeDef AddMember(params MemberDef[] members) =>
			this.With(
				members: this.Members.AddRange(members.Where(m => m is object)));
		public TypeDef AddImplements(params SpecializedType[] interfaces) =>
			this.With(
				implements: this.Implements.AddRange(interfaces));
	}
}
