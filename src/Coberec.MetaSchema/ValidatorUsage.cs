using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.MetaSchema
{
    public class ValidatorUsage
    {
        public string Name { get; }
        public Dictionary<string, Newtonsoft.Json.Linq.JToken> Arguments { get; }
        /// array of fields that the validator is applied to. If it's empty it's applied to the entire object (`this`)
        public ImmutableArray<string> ForFields { get; }
        public int? DirectiveIndex { get; }
        public int? FieldIndex { get; }

        public ValidatorUsage(string name, Dictionary<string, Newtonsoft.Json.Linq.JToken> args, IEnumerable<string> forFields, int? directiveIndex = null, int? fieldIndex = null)
        {
            this.Name = name;
            this.Arguments = args;
            this.ForFields = forFields.ToImmutableArray();
            this.DirectiveIndex = directiveIndex;
            this.FieldIndex = fieldIndex;
        }
    }
}
