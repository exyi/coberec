using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.CoreLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Coberec.MetaSchema
{
    public class Directive: ITokenFormatable
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
        private FmtToken FormatArgs() => FmtToken.Join(", ", Args.Properties().Select(p => FmtToken.Concat(p.Name, ": ", FormatArg(p.Value))), FmtToken.IntegerTokenMap());
        public FmtToken Format() =>
            Args.Count == 0 ? FmtToken.Concat("@", Name) :
            FmtToken.Concat("@", Name, "(", FormatArgs(), ")")
                    .WithTokenNames("", "name", "", "args", "");

        public override string ToString() => Format().ToString();
    }
}
