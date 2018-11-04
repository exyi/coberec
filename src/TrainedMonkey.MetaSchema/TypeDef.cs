using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace TrainedMonkey.MetaSchema
{
    public class TypeDef
    {
        public TypeDef(string name, IEnumerable<Directive> directives, TypeDefCore core)
        {
            Name = name;
            Directives = directives.ToImmutableArray();
            Core = core;
        }

        public string Name { get; }
        public ImmutableArray<Directive> Directives { get; }
        public TypeDefCore Core { get; }

        FormatResult FormatDirectives() => FormatResult.Concat(Directives.Select(d => FormatResult.Concat(d.Format(), " ")));
        string Keyword() => Core.Match(_ => "scalar", _ => "union", _ => "interface", _ => "type");

        public FormatResult Format() => FormatResult.Concat(Keyword(), " ", Name, " ", Core.Format(FormatDirectives()));
        public override string ToString() => Format().ToString();
    }
}
