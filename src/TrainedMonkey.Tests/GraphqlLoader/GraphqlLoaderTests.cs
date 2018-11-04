using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Newtonsoft.Json;
using TrainedMonkey.MetaSchema;
using TrainedMonkey.Tests.TestGens;
using Xunit;

namespace TrainedMonkey.Tests.GraphqlLoader
{
    public class GraphqlLoaderTests
    {
        public GraphqlLoaderTests()
        {
            Arb.Register(typeof(MyArbs));
        }

        [Property]
        public void ParseTypeName(GraphqlName a)
        {
            var normalName = Helpers.ParseTypeRef(a.Name);
            var actualType = normalName as TypeRef.ActualTypeCase;
            Assert.NotNull(actualType);
            Assert.Equal(actualType.TypeName, a.Name);
        }

        [Property]
        public void ParseNullableTypeName(GraphqlName a)
        {
            var normalName = Helpers.ParseTypeRef(a.Name + "!");
            var actualType = (normalName as TypeRef.NullableTypeCase)?.Type as TypeRef.ActualTypeCase;
            Assert.NotNull(actualType);
            Assert.Equal(actualType.TypeName, a.Name);
        }

        [Property]
        public void ParseListTypeName(GraphqlName a)
        {
            var normalName = Helpers.ParseTypeRef($"[{a}]");
            var actualType = (normalName as TypeRef.ListTypeCase)?.Type as TypeRef.ActualTypeCase;
            Assert.NotNull(actualType);
            Assert.Equal(actualType.TypeName, a.Name);
        }

        [Property]
        public void ParseTypeField(GraphqlName name, GraphqlName type)
        {
            var f = Helpers.ParseTypeField($"{name}: {type}");
            Assert.Equal(f.Name, name.Name);
            var actualType = f.Type as TypeRef.ActualTypeCase;
            Assert.Equal(actualType.TypeName, type.Name);
        }

        [Property]
        public void ParseTypeFieldWithDirectives(GraphqlName name, GraphqlName type, GraphqlName dir2name, GraphqlName argName)
        {
            var f = Helpers.ParseTypeField($"{name}: {type} @dir1(lol: \"ahoj\") @{dir2name}({argName}: 12) @dir3");
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
        public void ParseType(GraphqlName name, (GraphqlName, GraphqlName, GraphqlName[])[] fields)
        {
            string makeField(string n, string t, IEnumerable<string> dirs) =>
                $"{n}:{t} " + string.Join(" ", dirs.Select(d => $"@{d}"));

            var fff = fields.Select(f => makeField(f.Item1.Name, f.Item2.Name, f.Item3.Select(n => n.Name)));
            var type = $"type {name} implements Ifc123 @directive123 {{ {string.Join(",", fff)} }}";

            var typeDef = Helpers.ParseTypeDef(type);
            Assert.Equal(name.Name, typeDef.Name);
            Assert.Equal(new [] { ("directive123", "{}") }, typeDef.Directives.Select(d => (d.Name, d.Args.ToString(Formatting.None))));
            // Assert.True(fields.Length < 1);
            var core = typeDef.Core as TypeDefCore.CompositeCase;
            Assert.NotNull(core);
            Assert.Equal("Ifc123", (core.Implements.Single() as TypeRef.ActualTypeCase).TypeName);
            foreach (var ((ename, etype, edirs), tree) in Microsoft.FSharp.Collections.SeqModule.Zip(fields, core.Fields))
            {
                Assert.Equal(etype.Name, ((TypeRef.ActualTypeCase)tree.Type).TypeName);
                Assert.Equal(ename.Name, tree.Name);
                Assert.Equal(edirs.Select(a => (a.Name, "{}")), tree.Directives.Select(d => (d.Name, d.Args.ToString(Formatting.None))));
            }
        }
    }
}
