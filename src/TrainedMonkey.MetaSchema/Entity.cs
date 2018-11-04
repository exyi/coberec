using System;
using System.Collections.Generic;
using System.Linq;

namespace TrainedMonkey.MetaSchema
{
    public class Entity
    {
        public Entity(string name, string description, IEnumerable<Directive> directives, TypeRef type)
        {
            Name = name;
            Description = description;
            Directives = directives;
            Type = type;
        }
        public string Name { get; }
        public string Description { get; }
        public IEnumerable<Directive> Directives { get; }
        public TypeRef Type { get; }
    }
}
