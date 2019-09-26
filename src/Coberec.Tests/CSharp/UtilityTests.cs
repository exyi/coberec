using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis.CSharp;
using Coberec.CSharpGen;
using Coberec.CSharpGen.Emit;
using Coberec.Tests.TestGens;
using Xunit;
using Coberec.ExprCS;

namespace Coberec.Tests.CSharp
{
    public class UtilityTests
    {
        public UtilityTests()
        {
            Arb.Register(typeof(MyArbs));
        }

        [Property(EndSize = 500)]
        public void IdentifierSanitization(AnyString anyString)
        {
            var sanitized = NameSanitizer.SanitizeCsharpName(anyString.Val, null);
            Assert.True(SyntaxFacts.IsValidIdentifier(sanitized));
            if (SyntaxFacts.IsValidIdentifier(anyString.Val))
            {
                Assert.Equal(sanitized, anyString.Val);
            }
        }

        [Property(EndSize = 500)]
        public void IdentifierSanitizationOfGraphqlName(GraphqlName s)
        {
            var sanitized = NameSanitizer.SanitizeCsharpName(s.Name, null);
            Assert.True(SyntaxFacts.IsValidIdentifier(sanitized));
            Assert.True(SyntaxFacts.IsValidIdentifier(s.Name));
            Assert.Equal(sanitized, s.Name);
        }
    }
}
