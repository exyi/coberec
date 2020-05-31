using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;

namespace Coberec.MetaSchema
{
    public class TypeDef: ITokenFormatable
    {
        public TypeDef(string name, IEnumerable<Directive> directives, TypeDefCore core, string description = null)
        {
            Name = name;
            Directives = directives.ToImmutableArray();
            Core = core;
            Description = description;
        }

        public string Name { get; }
        public ImmutableArray<Directive> Directives { get; }
        public TypeDefCore Core { get; }
        public string Description { get; }

        FmtToken FormatDirectives() =>
            FmtToken.Concat(Directives.Select(d => FmtToken.Concat(d, " ")))
                .WithIntegerTokenMap()
                .Name("directives");
        string Keyword() => Core.Match(_ => "scalar", _ => "union", _ => "interface", _ => "type");

        public FmtToken Format() => FmtToken.Concat(Keyword(), " ", Name, " ", Core.Format(FormatDirectives()))
                                            .WithTokenNames("core", "", "name", "", null);
        public override string ToString() => Format().ToString();
    }
}
