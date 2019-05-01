using System;
using System.Collections.Generic;
using System.Linq;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests
{
    public class SymbolReadingTests
    {
        MetadataContext defaultContext = MetadataContext.Create("MyMainModule");
        OutputChecker check = new OutputChecker("testoutput");

        [Fact]
        public void LoadCoreTypes()
        {
            var stringT = defaultContext.FindTypeDef(typeof(string));
            var dateTimeT = defaultContext.FindTypeDef("System.DateTime");

            Assert.True(stringT.IsSealed);
            Assert.Equal(0, stringT.GenericArgCount);
            Assert.Equal("String", stringT.Name);
            Assert.Equal("DateTime", dateTimeT.Name);
            Assert.Equal(TypeOrNamespace.NamespaceSignature(new NamespaceSignature("System", null)), dateTimeT.Parent);
            Assert.Equal(dateTimeT.Parent, stringT.Parent);

            var enumerableT = defaultContext.FindTypeDef(typeof(IEnumerable<>));

            check.CheckJsonObject(new { stringT, dateTimeT, enumerableT });
        }

        [Fact]
        public void LoadCoreMethods()
        {
            var stringT = defaultContext.FindTypeDef(typeof(string));

            var methods = defaultContext.GetMemberMethods(stringT).ToArray();

            var toUpperInvariantM = methods.Single(s => s.Name == "ToUpperInvariant");

            check.CheckJsonObject(new { toUpperInvariantM });
        }
    }
}
