using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Coberec.JsonSerialization
{
    public static class ModelSerializer
    {
        public static JsonSerializerSettings GetSerializerSettings()
        {
            var s = new JsonSerializerSettings();
            s.Converters.Add(new UnionJsonConverter());
            s.ContractResolver = new DefaultContractResolver {
                NamingStrategy = new CamelCaseNamingStrategy()
            };
            s.Formatting = Formatting.Indented;
            return s;
        }

        public static string SerializeObject(object o)
        {
            return JsonConvert.SerializeObject(o, GetSerializerSettings());
        }

        public static T DeserializeObject<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, GetSerializerSettings());
        }
    }
}
