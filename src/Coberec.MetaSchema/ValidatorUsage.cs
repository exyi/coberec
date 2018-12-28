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
        public ImmutableArray<string> ForFields { get; }
        public ValidatorUsage(string name, Dictionary<string, Newtonsoft.Json.Linq.JToken> args, IEnumerable<string> forFields)
        {
            this.Name = name;
            this.Arguments = args;
            this.ForFields = forFields.ToImmutableArray();
        }
    }
}
