using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using Coberec.CSharpGen;
using Coberec.MetaSchema;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp;
using FsCheck.Xunit;
using System.Reflection;
using FsCheck;
using Newtonsoft.Json.Linq;
using CheckTestOutput;
using Coberec.CoreLib;

namespace Coberec.Tests.CoreLib
{
    public class TestTraversableObject: ITraversableObject
    {
        public Dictionary<string, object> Props = new Dictionary<string, object>();

        public ImmutableArray<string> Properties => Props.Keys.OrderBy(a => a).ToImmutableArray();

        public int PropertyCount => Props.Count;

        public object this[string prop]
        {
            get => Props[prop];
            set => Props[prop] = value;
        }

        public void Add(string k, object o) => Props.Add(k, o);

        public object GetValue(int propIndex)
        {
            var p = Properties[propIndex];
            return Props[p];
        }
    }

    public class TraversableObjectTests
    {
        [Fact]
        public void GetObjectProperty()
        {
            var obj = new TestTraversableObject {
                ["A"] = 1,
                ["B"] = new TestTraversableObject {
                    ["C"] = "lol"
                }
            };

            Assert.Equal(1, TraversableObject.GetValue(obj, "A"));
            Assert.Equal("lol", TraversableObject.GetValue(TraversableObject.GetValue(obj, "B"), "C"));

            var exception = Assert.ThrowsAny<Exception>(() => TraversableObject.GetValue(obj, "no-property"));
            Assert.Contains("no-property", exception.Message);
        }

        [Fact]
        public void GetArrayProperty()
        {
            var list = ImmutableArray.Create(1, 2, 3);
            var obj = new TestTraversableObject {
                ["A"] = 1,
                ["B"] = list
            };

            Assert.Equal(1, TraversableObject.GetValue(obj, "A"));
            Assert.Equal(2, TraversableObject.GetValue(TraversableObject.GetValue(obj, "B"), "1"));

            Assert.ThrowsAny<Exception>(() => TraversableObject.GetValue(list, "3"));
        }

        [Fact]
        public void FindPath()
        {
            var obj = new TestTraversableObject {
                ["A"] = 1,
                ["B"] = ImmutableArray.Create(new TestTraversableObject {
                    ["C"] = "lol",
                    ["D"] = 1
                }),
                ["X"] = 2
            };

            Assert.Equal(
                new [] { "X" },
                TraversableObject.FindObjects(obj, (int a) => a == 2).Select(x => string.Join(".", x))
            );
            Assert.Equal(
                new [] { "A", "B.0.D" },
                TraversableObject.FindObjects(obj, (int a) => a == 1).Select(x => string.Join(".", x))
            );
            Assert.Equal(
                new [] { "B.0.C" },
                TraversableObject.FindObjects(obj, (string a) => a == "lol").Select(x => string.Join(".", x))
            );
            Assert.Equal(
                new [] { "A", "B", "X" },
                TraversableObject.FindObjects(obj, (object a) => true).Select(x => string.Join(".", x))
            );
            Assert.Equal(
                new [] { "A", "B", "B.0", "B.0.C", "B.0.D", "X" },
                TraversableObject.FindObjects(obj, (object a) => true, true).Select(x => string.Join(".", x))
            );
        }
    }
}
