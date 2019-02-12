using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.CoreLib;

namespace Coberec.MetaSchema
{
    public static class SemanticSchemaValidation
    {
        public static ValidationErrors ValidateTypeReferences(this DataSchema schema, IEnumerable<string> predefinedTypes)
        {
            // note that predefined types can not collide with defined ones, they are overridden
            var typeNames = new HashSet<string>(schema.Types.Select(tt => tt.Name).Concat(predefinedTypes));

            ValidationErrors validateRef(TypeRef t) =>
                t.Match(actual: tt => typeNames.Contains(tt.TypeName) ? ValidationErrors.Valid : ValidationErrors.Create($"Type '{tt.TypeName}' is not defined."),
                        nullable: tt => validateRef(tt.Type).Nest("type"),
                        list: tt => validateRef(tt.Type).Nest("type")
                );

            ValidationErrors validateFields(TypeField field) => validateRef(field.Type).Nest("type");

            var typeErrors = schema.Types.ValidateList(type =>
                type.Core.Match(
                    primitive: p => ValidationErrors.Valid,
                    union: u => u.Options.ValidateList(validateRef).Nest("options"),
                    @interface: i => i.Fields.ValidateList(validateFields).Nest("fields"),
                    composite: c => ValidationErrors.Join(
                        c.Implements.ValidateList(validateRef).Nest("implements"),
                        c.Fields.ValidateList(validateFields).Nest("fields")
                    )
                ).Nest("core")).Nest("types");

            return ValidationErrors.Join(typeErrors);
        }
    }
}
