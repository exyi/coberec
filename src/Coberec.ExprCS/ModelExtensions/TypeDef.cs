using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;

namespace Coberec.ExprCS
{
	/// <summary> Represents a definition of a type. Apart from the (<see cref="TypeDef.Signature" />) contains the type members (<see cref="TypeDef.Members" />), attributes (TODO) and list of interface implementations (<see cref="Implements" />). </summary>
	public sealed partial class TypeDef
	{
		static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, TypeDef obj)
		{
			for (int i = 0; i < obj.Members.Length; i++)
			{
				var sgn = obj.Members[i].Signature;
				if (sgn.DeclaringType() != obj.Signature)
					e.Add(ValidationErrors.Create($"Can not contain member {sgn} with declaring type {sgn.DeclaringType()}.").Nest("declaringType").Nest("signature").Nest(i.ToString()).Nest("members"));
			}
		}

		public static TypeDef Empty(TypeSignature signature) => new TypeDef(signature, null, ImmutableArray<SpecializedType>.Empty, ImmutableArray<MemberDef>.Empty);

		public TypeDef AddMember(params MemberDef[] members) =>
			this.With(
				members: this.Members.AddRange(members.Where(m => m is object)));
		public TypeDef AddImplements(params SpecializedType[] interfaces) =>
			this.With(
				implements: this.Implements.AddRange(interfaces));
	}
}
