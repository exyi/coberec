using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class TypeReference
    {
        /// <summary> Says if the type is object-like. Says `true` for classes, arrays, functions. Says `false` for value types, pointers. And says `null` for stuff that is unknown - generic parameters and by-ref types. </summary>
        public bool? IsReferenceType => this.Match<bool?>(
            specializedType: t => !t.Item.Type.IsValueType,
            arrayType: _ => true,
            byReferenceType: _ => null,
            pointerType: _ => false,
            genericParameter: _ => null,
            functionType: _ => true
        );

        public static implicit operator TypeReference(TypeSignature signature) => new SpecializedType(signature, ImmutableArray<TypeReference>.Empty);

        /// <summary> If the type is a <see cref="ByReferenceTypeCase" /> it is unwrapped. Otherwise it is unchanged. </summary>
        public TypeReference UnwrapReference() =>
            this is ByReferenceTypeCase byref ? byref.Item.Type
                                              : this;

        // TODO: this should be generated
        public override string ToString() => this.Match(
            x => x.Item.ToString(),
            x => x.Item.ToString(),
            x => x.Item.ToString(),
            x => x.Item.ToString(),
            x => x.Item.ToString(),
            x => x.Item.ToString()
        );
    }
}
