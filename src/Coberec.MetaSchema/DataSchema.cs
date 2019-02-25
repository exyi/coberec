using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Coberec.CoreLib;

namespace Coberec.MetaSchema
{
    public sealed class DataSchema
    {
        static ValidationErrors ValidateName(string name)
        {
            if (name.Length == 0)
                return ValidationErrors.Create("Name can't be empty.");
            else if (!char.IsLetter(name[0]))
                return ValidationErrors.Create("Name must start with a letter");
            else if (!name.All(c => char.IsLetterOrDigit(c) || c == '_'))
                return ValidationErrors.Create("Name must consist only of letters, digits and underscores");
            else if (!name.All(c => c < 128))
                return ValidationErrors.Create("Name must consist only of ASCII characters");
            else
                return null;
        }
        static ValidationErrors ValidateDirectives(ImmutableArray<Directive> directives)
        {
            return ValidationErrors.Join(
                directives.Select((d, i) => ValidateName(d.Name).Nest("name").Nest(i.ToString())).ToArray()
            ).Nest("directives");
        }
        static ValidationErrors ValidateFields(ImmutableArray<TypeField> fields)
        {
            return ValidationErrors.Join(
                fields
                .GroupBy(f => f.Name)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .Select(field =>
                    ValidationErrors.CreateField(new [] { fields.IndexOf(field).ToString(), "name" }, $"Non-unique field name: {field.Name}")
                )
                .Concat(fields.Select((f, i) =>
                    ValidationErrors.Join(
                        ValidateName(f.Name).Nest("name"),
                        ValidateDirectives(f.Directives)
                    ).Nest(i.ToString())
                ))
                .ToArray())
                .Nest("fields");
        }
        public static ValidationErrors Validate(ImmutableArray<Entity> entities, ImmutableArray<TypeDef> types)
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
                    {
                        result.Add(ValidationErrors.CreateField(new []{ "types", index.ToString(), "name" }, $"Name of this type is not unique."));
                    }
                }
            }

            foreach (var (index, type) in types.Select((a, i) => (i, a)))
            {
                result.Add(ValidateName(type.Name).Nest("name").Nest(index.ToString()).Nest("types"));
                if (type.Core is TypeDefCore.CompositeCase composite)
                {
                    result.Add(ValidateFields(composite.Fields).Nest("core").Nest(index.ToString()).Nest("types"));
                }
                else if (type.Core is TypeDefCore.InterfaceCase ifc)
                {
                    result.Add(ValidateFields(ifc.Fields).Nest("core").Nest(index.ToString()).Nest("types"));
                }
                else if (type.Core is TypeDefCore.UnionCase union)
                {
                    result.Add(ValidationErrors.Join(
                        union.Options.Select((o, i) =>
                            (o is TypeRef.ActualTypeCase actO ?
                            ValidateName(actO.TypeName) :
                            ValidationErrors.Create("Union case can't have any modifiers (list, non-null)."))
                            .Nest(i.ToString()).Nest("options").Nest("core").Nest(index.ToString()).Nest("types")
                        )
                        .ToArray()
                    ));
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
            Validate(entities, types).ThrowErrors("Could not create DataSchema");

            Entities = entities;
            Types = types;
        }

        private DataSchema(
            NoNeedForValidationSentinel _,
            ImmutableArray<Entity> entities,
            ImmutableArray<TypeDef> types
        )
        {
            Validate(entities, types).ThrowErrors("Could not create DataSchema");

            Entities = entities;
            Types = types;
        }

        public static ValidationResult<DataSchema> Create(
            IEnumerable<Entity> entities,
            IEnumerable<TypeDef> types
        ) => Create(entities.ToImmutableArray(), types.ToImmutableArray());

        public static ValidationResult<DataSchema> Create(
            ImmutableArray<Entity> entities,
            ImmutableArray<TypeDef> types
        )
        {
            var validation = Validate(entities, types);
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
