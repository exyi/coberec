using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TrainedMonkey.MetaSchema
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
    }
}
