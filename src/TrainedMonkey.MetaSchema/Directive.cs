using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Coberec.MetaSchema
{
    public class Directive
    {
        public Directive(string name, JObject args)
        {
            Name = name;
            Args = args;
        }

        public string Name { get; }
        public JObject Args { get; }

        public static string FormatArg(JToken token)
        {
            using(var s = new System.IO.StringWriter())
            using(var j = new GraphqlJsonWriter(s))
            {
                token.WriteTo(j);
                return s.ToString();
            }
        }
        private FormatResult FormatArgs() => FormatResult.Join(", ", Args.Properties().Select(p => FormatResult.Concat(p.Name, ": ", FormatArg(p.Value))));
        public FormatResult Format() =>
            Args.Count == 0 ? FormatResult.Concat("@", Name) :
            FormatResult.Concat("@", Name, "(", FormatArgs(), ")");

        public override string ToString() => Format().ToString();
    }
}
