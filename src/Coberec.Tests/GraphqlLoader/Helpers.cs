using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Newtonsoft.Json.Linq;
using Coberec.MetaSchema;

namespace Coberec.Tests.GraphqlLoader
{
    public static class Helpers
    {
        public static DataSchema ParseSchema(string code, bool invertNonNull = false) =>
            Coberec.GraphqlLoader.GraphqlLoader.LoadFromGraphQL(new [] { ("testfile.gql", new Lazy<string>(code)) }, invertNonNull).schema;

        public static TypeDef ParseTypeDef(string code, bool invertNonNull = false) =>
            ParseSchema(code, invertNonNull).Types.Single();

        public static TypeField ParseTypeField(string code, bool invertNonNull = false) =>
            ((TypeDefCore.CompositeCase)ParseTypeDef($"type A {{ {code} }}", invertNonNull).Core).Fields.Single();
        public static TypeRef ParseTypeRef(string code, bool invertNonNull = false) =>
            ParseTypeField($"f1: {code}", invertNonNull).Type;
        public static ImmutableArray<Directive> ParseDirectives(string code, bool invertNonNull = false) =>
            ParseTypeField($"f1: Int {code}", invertNonNull).Directives;
        public static JToken ParseValueToJson(string code, bool invertNonNull = false) =>
            ParseDirectives($"@d1(value1: {code})", invertNonNull).Single().Args.Property("value1").Value;
    }
}
