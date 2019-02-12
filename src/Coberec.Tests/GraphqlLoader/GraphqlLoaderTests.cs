using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Newtonsoft.Json;
using Coberec.MetaSchema;
using Coberec.Tests.TestGens;
using Coberec.CSharpGen;
using Xunit;
using Seq=Microsoft.FSharp.Collections.SeqModule;

namespace Coberec.Tests.GraphqlLoader
{
    public class GraphqlLoaderTests
    {
        public GraphqlLoaderTests()
        {
            Arb.Register(typeof(MyArbs));
        }

        [Property]
        public void ParseTypeName(GraphqlName a, bool invertNulls)
        {
            var normalName = Helpers.ParseTypeRef(invertNulls ? a.Name : a.Name + "!", invertNulls);
            var actualType = normalName as TypeRef.ActualTypeCase;
            Assert.NotNull(actualType);
            Assert.Equal(actualType.TypeName, a.Name);
        }

        [Property]
        public void ParseNullableTypeName(GraphqlName a, bool invertNulls)
        {
            var normalName = Helpers.ParseTypeRef(invertNulls ? a.Name  + "!" : a.Name, invertNulls);
            var actualType = (normalName as TypeRef.NullableTypeCase)?.Type as TypeRef.ActualTypeCase;
            Assert.NotNull(actualType);
            Assert.Equal(actualType.TypeName, a.Name);
        }

        [Property]
        public void ParseListTypeName(GraphqlName a, bool invertNulls)
        {
            var normalName = Helpers.ParseTypeRef(invertNulls ? $"[{a}]" : $"[{a}!]!", invertNulls);
            var actualType = (normalName as TypeRef.ListTypeCase)?.Type as TypeRef.ActualTypeCase;
            Assert.NotNull(actualType);
            Assert.Equal(actualType.TypeName, a.Name);
        }

        [Property]
        public void ParseTypeField(GraphqlName name, GraphqlName type)
        {
            var f = Helpers.ParseTypeField($"{name}: {type}!");
            Assert.Equal(f.Name, name.Name);
            var actualType = f.Type as TypeRef.ActualTypeCase;
            Assert.Equal(actualType.TypeName, type.Name);
        }

        [Property]
        public void ParseTypeFieldWithDirectives(GraphqlName name, GraphqlName type, GraphqlName dir2name, GraphqlName argName)
        {
            var f = Helpers.ParseTypeField($"{name}: {type} @dir1(lol: \"ahoj\") @{dir2name}({argName}: 12) @dir3", invertNonNull: true);
            Assert.Equal(f.Name, name.Name);
            var actualType = f.Type as TypeRef.ActualTypeCase;
            Assert.Equal(actualType.TypeName, type.Name);

            var dir1 = f.Directives.Single(d => d.Name == "dir1");
            var dir2 = f.Directives.Single(d => d.Name == dir2name.Name);
            var dir3 = f.Directives.Single(d => d.Name == "dir3");

            Assert.Equal("{\"lol\":\"ahoj\"}",dir1.Args.ToString(Formatting.None));
            Assert.Equal("{\""+ argName +"\":12}",dir2.Args.ToString(Formatting.None));
            Assert.Equal("{}",dir3.Args.ToString(Formatting.None));
        }

        [Property]
        public void ParseType(GraphqlName name, (GraphqlName name, GraphqlName type, GraphqlName[] directives)[] fields, GraphqlName implements, GraphqlName directive)
        {
            // filter out fields with colliding names
            fields = fields.DistinctBy(f => f.name.Name).ToArray();

            string makeField(string n, string t, IEnumerable<string> dirs) =>
                $"{n}:{t} " + string.Join(" ", dirs.Select(d => $"@{d}"));

            var fff = fields
                      .Select(f => makeField(f.name.Name, f.type.Name, f.directives.Select(n => n.Name)));
            var type = $"type {name} implements {implements} @{directive} {{ {string.Join(",", fff)} }}";

            var typeDef = Helpers.ParseTypeDef(type, invertNonNull: true);
            // Console.WriteLine(typeDef.ToString());
            Assert.Equal(name.Name, typeDef.Name);
            Assert.Equal(new [] { (directive.Name, "{}") }, typeDef.Directives.Select(d => (d.Name, d.Args.ToString(Formatting.None))));
            // Assert.True(fields.Length < 1);
            var core = typeDef.Core as TypeDefCore.CompositeCase;
            Assert.NotNull(core);
            Assert.Equal(implements.Name, (core.Implements.Single() as TypeRef.ActualTypeCase).TypeName);
            foreach (var ((ename, etype, edirs), tree) in Seq.Zip(fields, core.Fields))
            {
                Assert.Equal(etype.Name, ((TypeRef.ActualTypeCase)tree.Type).TypeName);
                Assert.Equal(ename.Name, tree.Name);
                Assert.Equal(edirs.Select(a => (a.Name, "{}")), tree.Directives.Select(d => (d.Name, d.Args.ToString(Formatting.None))));
            }
        }

        [Property]
        public void ParseArgumentValue(Newtonsoft.Json.Linq.JToken json)
        {
            // Console.WriteLine(json.ToString(Formatting.Indented));
            var json2 = Helpers.ParseValueToJson(Directive.FormatArg(json));
            Assert.Equal(json.ToString(Formatting.None), json2.ToString(Formatting.None));
        }
    }
}
