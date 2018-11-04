using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json.Linq;
using TrainedMonkey.MetaSchema;

namespace TrainedMonkey.Tests.GraphqlLoader
{
    public static class Helpers
    {
        public static DataSchema ParseSchema(string code) =>
            TrainedMonkey.GraphqlLoader.GraphqlLoader.LoadFromGraphQL(new [] { ("testfile.gql", new Lazy<string>(code)) });

        public static TypeDef ParseTypeDef(string code) =>
            ParseSchema(code).Types.Single();

        public static TypeField ParseTypeField(string code) =>
            ((TypeDefCore.CompositeCase)ParseTypeDef($"type A {{ {code} }}").Core).Fields.Single();
        public static TypeRef ParseTypeRef(string code) =>
            ParseTypeField($"f1: {code}").Type;
        public static ImmutableArray<Directive> ParseDirectives(string code) =>
            ParseTypeField($"f1: Int {code}").Directives;
        public static JToken ParseValueToJson(string code) =>
            ParseDirectives($"@d1(value1: {code})").Single().Args.Property("value1").Value;
    }
}
