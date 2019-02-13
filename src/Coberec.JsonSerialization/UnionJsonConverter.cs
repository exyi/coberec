using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Newtonsoft.Json;

namespace Coberec.JsonSerialization
{
    public class UnionJsonConverter : JsonConverter
    {
        class UnionMappingInfo
        {
            public Type Type { get; set; }
            public Dictionary<Type, (string name, Func<object, object> extractObject)> SerializationMapping { get; set; }
            public Dictionary<string, (Type, Func<object, object> caseConstructor)> DeserializationMapping { get; set; }
        }
        static ConcurrentDictionary<Type, UnionMappingInfo> cache = new ConcurrentDictionary<Type, UnionMappingInfo>();
        static ParameterExpression objParam = Expression.Parameter(typeof(object));
        static UnionMappingInfo GetMappingInfo(Type type)
        {
            return cache.GetOrAdd(type, t => {
                if (t.IsNested) return GetMappingInfo(t.DeclaringType);
                if (!t.IsAbstract) return null;
                var cases = t.GetNestedTypes();
                if (cases.Length == 0 || cases.Any(c => !c.Name.EndsWith("Case"))) return null;

                var names = cases.Select(c => c.Name.Remove(c.Name.Length - 4)).ToArray();
                var serMap = cases.Zip(names, (c, n) => new {
                    c,
                    n,
                    extractObject = Expression.Lambda<Func<object, object>>(Expression.Property(Expression.Convert(objParam, c), c.GetProperties(BindingFlags.Public | BindingFlags.Instance).Single()), new [] { objParam }).Compile()
                }).ToDictionary(x => x.c, x => (x.n, x.extractObject));

                var desMap = cases.Zip(names, (c, n) => {
                    var ctor = c.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
                    var innerType = ctor.GetParameters().Single().ParameterType;
                    return new {
                        c,
                        n,
                        innerType,
                        caseCtor = Expression.Lambda<Func<object, object>>(Expression.New(ctor, Expression.Convert(objParam, innerType)), new [] { objParam }).Compile()
                    };
                }).ToDictionary(x => x.n, x => (x.innerType, x.caseCtor));

                return new UnionMappingInfo {
                    Type = t,
                    SerializationMapping = serMap,
                    DeserializationMapping = desMap
                };
            });
        }
        public override bool CanConvert(Type objectType)
        {
            return GetMappingInfo(objectType) != null;
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var mapping = GetMappingInfo(objectType);
            if (reader.TokenType != JsonToken.StartObject) throw new Exception("Expected object");
            reader.Read();
            if (reader.TokenType != JsonToken.PropertyName) throw new Exception("Expected property");
            var name = (string)reader.Value;

            var (innerType, ctor) = mapping.DeserializationMapping[name];

            reader.Read();
            var obj = serializer.Deserialize(reader, innerType);
            if (reader.TokenType != JsonToken.EndObject) throw new Exception("Expected end of object");
            reader.Read();
            return ctor(obj);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var type = value.GetType();
            var mapping = GetMappingInfo(type);
            var (name, extract) = mapping.SerializationMapping[type];

            writer.WriteStartObject();
            writer.WritePropertyName(name);
            serializer.Serialize(writer, extract(value));
            writer.WriteEnd();
        }
    }
}
