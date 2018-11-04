using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TrainedMonkey.MetaSchema
{
    public sealed class TypeField
    {
        public TypeField(string name, TypeRef type, string description, IEnumerable<Directive> directives)
        {
            Name = name;
            Type = type;
            Description = description;
            Directives = directives.ToImmutableArray();
        }

        public string Name { get; }
        public TypeRef Type { get; }
        public string Description { get; }
        public ImmutableArray<Directive> Directives { get; }

        private FormatResult FormatDirectives() => FormatResult.Concat(Directives.Select(d => FormatResult.Concat(" ", d.Format())));
        public FormatResult Format() => FormatResult.Concat(Name, ": ", Type.Format(), FormatDirectives());

        public override string ToString() => Format().ToString();
    }
}
