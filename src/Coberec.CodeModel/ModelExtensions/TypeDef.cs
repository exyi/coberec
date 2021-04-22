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
		static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, TypeDef t)
		{
			for (int i = 0; i < t.Members.Length; i++)
			{
				var sgn = t.Members[i].Signature;
				if (sgn.DeclaringType() != t.Signature)
					e.Add(ValidationErrors.Create($"Can not contain member {sgn} with declaring type {sgn.DeclaringType()}.").Nest("declaringType").Nest("signature").Nest(i.ToString()).Nest("members"));
			}
			if (t.Extends is SpecializedType { Type: var baseType })
			{
				if (!baseType.CanOverride)
					e.Add(ValidationErrors.Create($"Can not override {baseType}.").Nest("type").Nest("extends"));
				if (baseType.Kind == "interface")
					e.Add(ValidationErrors.Create($"Can not override {baseType}. Please use Implements property for interfaces.").Nest("type").Nest("extends"));
			}
			for (int i = 0; i < t.Implements.Length; i++)
			{
				var ifc = t.Implements[i];
				if (ifc.Type.Kind != "interface")
					e.Add(ValidationErrors.Create($"Can not implement type {ifc} as it is not an interface. Please use Extends property for inheritance.").Nest("type").Nest(i.ToString()).Nest("implements"));
			}
		}

		public static TypeDef Empty(TypeSignature signature, SpecializedType extends = null) => new TypeDef(signature, extends, ImmutableArray<SpecializedType>.Empty, ImmutableArray<MemberDef>.Empty);

		public TypeDef AddMember(params MemberDef[] members) =>
			this.With(
				members: this.Members.AddRange(members.Where(m => m is object)));
		public TypeDef AddImplements(params SpecializedType[] interfaces) =>
			this.With(
				implements: this.Implements.AddRange(interfaces));
	}
}
