using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;

namespace Coberec.ExprCS
{
    static class EmitExtensions
    {
        public static IAttribute CompilerGeneratedAttribute(this ICompilation compilation) =>
            new DefaultAttribute(
                KnownAttributes.FindType(compilation, KnownAttribute.CompilerGenerated),
                ImmutableArray<CustomAttributeTypedArgument<IType>>.Empty,
                ImmutableArray<CustomAttributeNamedArgument<IType>>.Empty
            );
    }
}
