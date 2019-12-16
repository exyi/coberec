using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests
{
    public class SymbolReadingTests
    {
        MetadataContext cx = MetadataContext.Create("MyMainModule");
        OutputChecker check = new OutputChecker("testoutput");

        [Fact]
        public void LoadCoreTypes()
        {
            var stringT = TypeSignature.FromType(typeof(string));
            var dateTimeT = cx.FindTypeDef("System.DateTime");

            Assert.False(stringT.CanOverride);
            Assert.Empty(stringT.TypeParameters);
            Assert.Equal("String", stringT.Name);
            Assert.Equal("DateTime", dateTimeT.Name);
            Assert.Equal(TypeOrNamespace.NamespaceSignature(NamespaceSignature.System), dateTimeT.Parent);
            Assert.Equal(dateTimeT.Parent, stringT.Parent);

            var enumerableT = TypeSignature.FromType(typeof(IEnumerable<>));

            check.CheckJsonObject(new { stringT, dateTimeT, enumerableT });
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(List<>))]
        public void LoadReflectionType(Type t)
        {
            var signature = TypeSignature.FromType(t);
            var methods = cx.GetMemberMethodDefs(signature).Where(m => m.Accessibility == Accessibility.APublic).Select(m => m.Name).Distinct().OrderBy(a => a).ToArray();
            var reflectionMethods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                     .Where(m => !m.IsSpecialName)
                                     .Select(m => m.Name).Distinct().OrderBy(a => a).ToArray();
            Assert.Equal(methods, reflectionMethods);
        }

        [Fact]
        public void LoadCoreMethods()
        {
            var stringT = TypeSignature.FromType(typeof(string));

            var methods = cx.GetMemberMethods(stringT.NotGeneric()).ToArray();

            var toUpperInvariantM = methods.Single(s => s.Name() == "ToUpperInvariant");

            check.CheckJsonObject(new { toUpperInvariantM });
        }
    }
}
