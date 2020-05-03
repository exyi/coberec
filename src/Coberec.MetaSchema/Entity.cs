using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;

namespace Coberec.MetaSchema
{
    public sealed class Entity: ITokenFormatable
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

        FmtToken FormatDirectives() => FmtToken.Concat(Directives.Select(d => FmtToken.Concat(" ", d.Format())));
        public FmtToken Format() => FmtToken.Concat(Name, "(id: ID): ", Type, "?", FormatDirectives());
        public override string ToString() => Format().ToString();
    }
}
