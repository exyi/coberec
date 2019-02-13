using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SampleProject.GeneratedSchema;
using Coberec.JsonSerialization;
using CheckTestOutput;

namespace Coberec.Tests
{
    public class SampleTests
    {
        CheckTestOutput.CheckTestOutput check = new CheckTestOutput.CheckTestOutput("testoutputs");
        [Fact]
        public void ObjectSerialization()
        {
            var obj = new TypeB(a: new ScalarA(""), new Uri("https://example.com/lol"), 3);
            var obj2 = ModelSerializer.DeserializeObject<TypeB>(ModelSerializer.SerializeObject(obj));
            Assert.Equal(obj, obj2);
        }

        [Fact]
        public void UnionSerialization()
        {
            var obj = UnionAB.TypeB(
                new TypeB(a: new ScalarA(""), new Uri("https://example.com/lol"), 3));
            var json = ModelSerializer.SerializeObject(obj);
            var obj2 = ModelSerializer.DeserializeObject<UnionAB>(json);
            Assert.Equal(obj, obj2);
            check.CheckString(json, fileExtension: "json");
        }
    }
}
