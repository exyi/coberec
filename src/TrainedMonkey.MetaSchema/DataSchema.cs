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

        public FormatResult Format(string rootType)
        {
            if (Entities.Length == 0 && Types.Any(t => t.Name == rootType))
                throw new InvalidOperationException($"Type with name '{rootType}' already exists in the schema so it can't be root type.");

            return FormatResult.Block(
                FormatResult.Block(Types.Select(e => e.Format())),
                Entities.Length == 0 && rootType != null ? "" :
                    FormatResult.Concat("type ", rootType, "{", FormatResult.Block(Entities.Select(s => s.Format())), "}")
            );
        }
        public override string ToString() => Format("Entities").ToString();
    }
}
