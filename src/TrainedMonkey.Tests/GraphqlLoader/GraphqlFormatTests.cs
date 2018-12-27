using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Coberec.MetaSchema;
using Coberec.Tests.TestGens;
using Xunit;

namespace Coberec.Tests.GraphqlLoader
{
    public class GraphqlFormatTests
    {
        public GraphqlFormatTests()
        {
            Arb.Register(typeof(MyArbs));
        }

        [Property]
        public void TestTypeRef(TypeRef t)
        {
            var clone = Helpers.ParseTypeRef(t.ToString());
            Assert.Equal(t.ToString(), clone.ToString());
        }

        [Property(Replay="487817991,296518929")]
        public void TestDirective(Directive directive)
        {
            var clone = Helpers.ParseDirectives(directive.ToString()).Single();
            Assert.Equal(directive.ToString(), clone.ToString());
        }

        [Property]
        public void TestTypeField(TypeField field)
        {
            var clone = Helpers.ParseTypeField(field.ToString());
            Assert.Equal(field.ToString(), clone.ToString());
        }

        [Property]
        public void TestTypeDef(TypeDef def)
        {
            // Console.WriteLine(def.ToString());
            var clone = Helpers.ParseTypeDef(def.ToString());
            Assert.Equal(def.ToString(), clone.ToString());
        }

        [Property]
        public void TestDataSchemaWithoutEntities(TypeDef[] types)
        {
            var schema = new DataSchema(Enumerable.Empty<Entity>(), types);
            var clone = Helpers.ParseSchema(schema.ToString());
            Assert.Equal(clone.ToString(), schema.ToString());
        }
    }
}
