using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Coberec.CoreLib.FmtToken;

namespace Coberec.Tests
{
    public class FormatResultTests
    {
        [Fact]
        public void SimpleHierarchy()
        {
            var result = Concat("a", Concat("b", Concat("c", 12))).ToString();
            Assert.Equal("abc12", result);
        }

        [Fact]
        public void SimpleBlocks()
        {
            var result = Block("a", Block("b", "c")).ToString();
            Assert.Equal("a\n\tb\n\tc\n", result);
        }

        [Fact]
        public void BlocksInExpressions()
        {
            var result1 = Concat("a {", Block("x", "y"), "}").ToString();
            var result2 = Concat("a {", Block("x"), Block("y"), "}").ToString();

            Assert.Equal("a {\n\tx\n\ty\n}", result1);
            Assert.Equal("a {\n\tx\n\n\ty\n}", result2);
        }
    }
}
