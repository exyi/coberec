using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.CoreLib;
using Coberec.CSharpGen;
using Xunit;
using CheckTestOutput;

namespace Coberec.Tests.CSharp
{
    public class ErrorsTests
    {
        CheckTestOutput.CheckTestOutput check = new CheckTestOutput.CheckTestOutput("testoutputs");

        public string GetCompilationErrors(string code, EmitSettings settings = null)
        {
            try
            {
                var (schema, mapper) = Coberec.GraphqlLoader.GraphqlLoader.LoadFromGraphQL(new [] { ("schema.gql", new Lazy<string>(code)) });

                try
                {
                    CSharpBackend.Build(schema, settings ?? CodeGeneratorTests.DefaultSettings);
                    Assert.True(false, "Expected that the build will fail.");
                    return null;
                }
                catch (ValidationErrorException error)
                {
                    throw new AggregateException(Coberec.GraphqlLoader.GraphqlLoader.MapErrors(error.Validation, mapper));
                }
            }
            catch (AggregateException e)
            {
                return String.Join("\n\n", e.InnerExceptions.Select(ee => ee.Message));
            }
            catch (GraphQLParser.Exceptions.GraphQLSyntaxErrorException e)
            {
                return e.Message;
            }
        }

        [Fact]
        public void BasicSyntaxErrors()
        {
            check.CheckString(GetCompilationErrors(@"
type D implements { }
"));
        }

        [Fact]
        public void UnsupportedStatements()
        {
            check.CheckString(GetCompilationErrors(@"
type Kokot {
    parametrizedFields(x: Int): String
}

input InputTypesDoesNotMakeSense { }


"));
        }
    }
}
