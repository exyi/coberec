using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.MetaSchema
{
    public sealed class Entity
    {
        public Entity(string name, string description, IEnumerable<Directive> directives, TypeRef type)
        {
            Name = name;
            Description = description;
            Directives = directives.ToImmutableArray();
            // TODO: must be actual type
            Type = type;
        }
        public string Name { get; }
        public string Description { get; }
        public ImmutableArray<Directive> Directives { get; }
        public TypeRef Type { get; }

        FormatResult FormatDirectives() => FormatResult.Concat(Directives.Select(d => FormatResult.Concat(" ", d.Format())));
        public FormatResult Format() => FormatResult.Concat(Name, "(id: ID): ", Type, "?", FormatDirectives());
        public override string ToString() => Format().ToString();
    }
}
