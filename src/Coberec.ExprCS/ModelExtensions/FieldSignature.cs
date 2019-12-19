using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Basic metadata about a field - <see cref="Name">, <see cref="Accessibility" />, <see cref="DeclaringType" />, ... </summary>
    public partial class FieldSignature
    {
        /// <summary> Fills in the generic parameters. </summary>
        public FieldReference Specialize(IEnumerable<TypeReference> typeArgs) =>
            new FieldReference(this, typeArgs.ToImmutableArray());

        /// <summary> Fills in the generic parameters from the declaring type. Useful when using the field inside it's declaring type. </summary>
        public FieldReference SpecializeFromDeclaringType() =>
            new FieldReference(this, this.DeclaringType.TypeParameters.EagerSelect(TypeReference.GenericParameter));

        public static implicit operator FieldReference(FieldSignature signature)
        {
            Assert.Empty(signature.DeclaringType.TypeParameters);
            return new FieldReference(signature, ImmutableArray<TypeReference>.Empty);
        }
    }
}
