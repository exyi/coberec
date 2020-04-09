using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.MetaSchema
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
            public override FmtToken Format(FmtToken directives) => directives;
        }

        public sealed class UnionCase: TypeDefCore
        {
            public UnionCase(IEnumerable<TypeRef> options)
            {
                // TODO: must be actual type
                // TODO: can't be empty
                // TODO: can't contain duplicates
                Options = options.ToImmutableArray();
            }

            public ImmutableArray<TypeRef> Options { get; }

            private protected override void Seal() {}
            public override FmtToken Format(FmtToken directives) =>
                FmtToken.Concat(
                    directives,
                    "= ",
                    FmtToken.Join(" | ", Options, FmtToken.IntegerTokenMap())
                ).WithTokenNames(null, "", "core.options");
        }

        public sealed class InterfaceCase: TypeDefCore
        {
            public InterfaceCase(IEnumerable<TypeField> fields)
            {
                Fields = fields.ToImmutableArray();
                if (Fields.GroupBy(f => f.Name).FirstOrDefault(g => g.Count() > 1) is var duplicateField && duplicateField != null)
                    throw new ArgumentException($"Interface can not contain fields with colliding names: {string.Join(", ", duplicateField)}", nameof(fields));
            }

            public ImmutableArray<TypeField> Fields { get; }

            private protected override void Seal() {}
            public override FmtToken Format(FmtToken directives) =>
                FmtToken.Concat(
                    directives,
                    "{",
                    FmtToken.Block(Fields.Select(f => FmtToken.Concat(f.Format(), ",")))
                            .WithIntegerTokenMap(),
                    "}"
                ).WithTokenNames(null, "", "core", "");
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
            public override FmtToken Format(FmtToken directives) =>
                FmtToken.Concat(
                    Implements.Length > 0 ? FmtToken.Concat(" implements ", FmtToken.Concat(Implements.Select(f => FmtToken.Concat(f, " "))).WithIntegerTokenMap())
                                          : "",
                    directives,
                    "{",
                    FmtToken.Block(Fields.Select(f => FmtToken.Concat(f, ",")))
                            .WithIntegerTokenMap(),
                    "}"
                )
                .WithTokenNames("core.implements", null, "", "core.fields", "");
        }

        public T Match<T>(Func<PrimitiveCase, T> primitive,
                          Func<UnionCase, T> union,
                          Func<InterfaceCase, T> @interface,
                          Func<CompositeCase, T> composite) =>
            this is PrimitiveCase p ? primitive(p) :
            this is UnionCase u ? union(u) :
            this is InterfaceCase i ? @interface(i) :
            this is CompositeCase c ? composite(c) :
            throw new Exception("wtf");

        public abstract FmtToken Format(FmtToken directives);
        public override string ToString() => Format("").ToString();
    }
}
