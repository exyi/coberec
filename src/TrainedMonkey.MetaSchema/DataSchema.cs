using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace TrainedMonkey.MetaSchema
{
    public sealed class DataSchema
    {
        public DataSchema(
            IEnumerable<Entity> entities,
            IEnumerable<TypeDef> types
        )
        {
            Entities = entities.ToImmutableArray();
            Types = types.ToImmutableArray();
        }

        public ImmutableArray<Entity> Entities { get; }
        public ImmutableArray<TypeDef> Types { get; }
    }
}
