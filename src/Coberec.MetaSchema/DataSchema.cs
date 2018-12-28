using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Coberec.CoreLib;

namespace Coberec.MetaSchema
{
    public sealed class DataSchema
    {
        public static ValidationErrors ValidateFields(ImmutableArray<Entity> entities, ImmutableArray<TypeDef> types)
        {
            // IEnumerable<string> getReferencedTypes(TypeRef t) =>
            //     t.Match(
            //         actual: x => new [] { x.TypeName },
            //         nullable: x => getReferencedTypes(x.Type),
            //         list: x => getReferencedTypes(x.Type)
            //     )
            // IEnumerable<string> getReferencedTypes(TypeDefCore t) =>
            //     t.Match(
            //         primitive: x => new string[0],
            //         union: x => x.Options.SelectMany(getReferencedTypes),
            //         @interface: x => x.Fields.SelectMany(f => getReferencedTypes(f.Type)),
            //         composite: x => x.Fields.SelectMany(f => getReferencedTypes(

            var declaredTypes = types.Select((a, index) => (a, index)).ToLookup(t => t.a.Name);
            var result = new List<ValidationErrors>();

            // Type names must be unique
            foreach (var d in declaredTypes)
            {
                if (d.Count() > 1)
                {
                    foreach(var (type, index) in d)
                        result.Add(ValidationErrors.CreateField(new []{ "entities", index.ToString(), "name" }, $"Name of this type is not unique."));
                }
            }

            return ValidationErrors.Join(result.ToArray());
        }
        public DataSchema(
            IEnumerable<Entity> entities,
            IEnumerable<TypeDef> types
        ) : this(
            entities.ToImmutableArray(),
            types.ToImmutableArray()
        ) {}
        public DataSchema(
            ImmutableArray<Entity> entities,
            ImmutableArray<TypeDef> types
        )
        {
            ValidateFields(entities, types).ThrowErrors("Could not create DataSchema");

            Entities = entities;
            Types = types;
        }

        private DataSchema(
            NoNeedForValidationSentinel _,
            ImmutableArray<Entity> entities,
            ImmutableArray<TypeDef> types
        )
        {
            ValidateFields(entities, types).ThrowErrors("Could not create DataSchema");

            Entities = entities;
            Types = types;
        }

        public ValidationResult<DataSchema> Create(
            IEnumerable<Entity> entities,
            IEnumerable<TypeDef> types
        ) => Create(entities.ToImmutableArray(), types.ToImmutableArray());

        public ValidationResult<DataSchema> Create(
            ImmutableArray<Entity> entities,
            ImmutableArray<TypeDef> types
        )
        {
            var validation = ValidateFields(entities, types);
            if (validation.IsValid())
                return ValidationResult.Create(new DataSchema(default(NoNeedForValidationSentinel), entities, types));
            else
                return ValidationResult.CreateErrors<DataSchema>(validation);
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
