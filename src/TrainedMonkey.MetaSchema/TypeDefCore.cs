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
            public override FormatResult Format(FormatResult directives) => directives;
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
            public override FormatResult Format(FormatResult directives) => FormatResult.Concat(directives, "= ", FormatResult.Join(" | ", Options.Select(f => f.Format())));
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
            public override FormatResult Format(FormatResult directives) => FormatResult.Concat(directives, "{", FormatResult.Block(Fields.Select(f => FormatResult.Concat(f.Format(), ","))), "}");
        }

        public sealed class CompositeCase: TypeDefCore
        {
            public CompositeCase(IEnumerable<TypeField> fields, IEnumerable<TypeRef> implements)
            {
                Fields = fields.ToImmutableArray();
                // TODO: must be actual types
                Implements = implements.ToImmutableArray();
                if (Fields.GroupBy(f => f.Name).FirstOrDefault(g => g.Count() > 1) is var duplicateField && duplicateField != null)
                    throw new ArgumentException($"Type can not contain fields with colliding names: {string.Join(", ", duplicateField)}", nameof(fields));
            }

            public ImmutableArray<TypeField> Fields { get; }
            public ImmutableArray<TypeRef> Implements { get; }
            private protected override void Seal() {}
            public override FormatResult Format(FormatResult directives) => FormatResult.Concat(
                Implements.Length > 0 ? FormatResult.Concat(" implements ", FormatResult.Concat(Implements.Select(f => FormatResult.Concat(f.Format(), " "))))
                                      : "",
                directives, "{", FormatResult.Block(Fields.Select(f => FormatResult.Concat(f.Format(), ","))), "}");
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

        public abstract FormatResult Format(FormatResult directives);
        public override string ToString() => Format("").ToString();
    }
}
