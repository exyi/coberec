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
    }
}
