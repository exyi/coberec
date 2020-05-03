using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;

namespace Coberec.MetaSchema
{
    public sealed class TypeField: ITokenFormatable
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

        private FmtToken FormatDirectives() => FmtToken.Concat(Directives.Select(d => FmtToken.Concat(" ", d)))
                                                       .WithIntegerTokenMap();
        public FmtToken Format() => FmtToken.Concat(Name, ": ", Type, FormatDirectives())
                                            .WithTokenNames("name", "", "type", "directives");

        public override string ToString() => Format().ToString();
    }
}
