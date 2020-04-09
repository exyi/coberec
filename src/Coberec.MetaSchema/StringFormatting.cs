using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Coberec.MetaSchema
{
    public sealed class GraphqlJsonWriter : Newtonsoft.Json.JsonTextWriter
    {
        public GraphqlJsonWriter(System.IO.TextWriter textWriter) : base(textWriter)
        {
            QuoteName = false;
            Formatting = Newtonsoft.Json.Formatting.None;
            // FloatFormatHandling = Newtonsoft.Json.FloatFormatHandling.
        }

        public override void WritePropertyName(string name, bool escape)
        {
            base.WritePropertyName(name, false);
        }
    }
}
