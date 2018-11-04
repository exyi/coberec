using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TrainedMonkey.MetaSchema
{
    public abstract class TypeDefCore
    {
        private protected abstract void Seal();

        public static TypeDefCore Primitive() => new PrimitiveCase();
        public static TypeDefCore Union(IEnumerable<TypeRef> options) => new UnionCase(options);
        public static TypeDefCore Interface(IEnumerable<TypeField> fields) => new InterfaceCase(fields);
        public static TypeDefCore Composite(IEnumerable<TypeField> fields, IEnumerable<TypeRef> implements) => new CompositeCase(fields, implements);

        public sealed class PrimitiveCase: TypeDefCore
        {
            public PrimitiveCase() {}
            private protected override void Seal() {}
        }

        public sealed class UnionCase: TypeDefCore
        {
            public UnionCase(IEnumerable<TypeRef> options)
            {
                Options = options.ToImmutableArray();
            }

            public ImmutableArray<TypeRef> Options { get; }

            private protected override void Seal() {}
        }

        public sealed class InterfaceCase: TypeDefCore
        {
            public InterfaceCase(IEnumerable<TypeField> fields)
            {
                Fields = fields.ToImmutableArray();
            }

            public ImmutableArray<TypeField> Fields { get; }

            private protected override void Seal() {}
        }

        public sealed class CompositeCase: TypeDefCore
        {
            public CompositeCase(IEnumerable<TypeField> fields, IEnumerable<TypeRef> implements)
            {
                Fields = fields.ToImmutableArray();
                Implements = implements.ToImmutableArray();
            }

            public ImmutableArray<TypeField> Fields { get; }
            public ImmutableArray<TypeRef> Implements { get; }
            private protected override void Seal() {}
        }
    }
}
