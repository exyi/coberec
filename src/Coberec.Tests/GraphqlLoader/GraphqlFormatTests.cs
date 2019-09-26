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
            var clone = Helpers.ParseTypeRef(t.ToString(), invertNonNull: true);
            Assert.Equal(t.ToString(), clone.ToString());
        }

        [Property]
        public void TestDirective(Directive directive)
        {
            var clone = Helpers.ParseDirectives(directive.ToString(), invertNonNull: true).Single();
            Assert.Equal(directive.ToString(), clone.ToString());
        }

        [Property]
        public void TestTypeField(TypeField field)
        {
            var clone = Helpers.ParseTypeField(field.ToString(), invertNonNull: true);
            Assert.Equal(field.ToString(), clone.ToString());
        }

        [Property]
        public void TestTypeDef(TypeDef def)
        {
            // Console.WriteLine(def.ToString());
            var clone = Helpers.ParseTypeDef(def.ToString(), invertNonNull: true);
            Assert.Equal(def.ToString(), clone.ToString());
        }

        [Property]
        public void TestDataSchema(DataSchema schema)
        {
            var clone = Helpers.ParseSchema(schema.ToString(), invertNonNull: true);
            Assert.Equal(clone.ToString(), schema.ToString());
        }
    }
}
