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
using System.Linq;
using Newtonsoft.Json.Linq;
using CheckTestOutput;
using Coberec.CoreLib;

namespace Coberec.Tests.CSharp
{
    public class CodeGeneratorTests
    {
        CheckTestOutput.CheckTestOutput check = new CheckTestOutput.CheckTestOutput("testoutputs");
        public CodeGeneratorTests()
        {
            Arb.Register(typeof(TestGens.MyArbs));
        }

        private const string ImplicitlyIncludedCode = @"
using System;
using Coberec.CoreLib;

namespace GeneratedProject {
    public static partial class Validators {
        public static ValidationErrors MySpecialStringValidator(int param1, string value) =>
            throw new NotImplementedException();
    }
}
";

        void CheckItCompiles(string code, string extension = "")
        {
            var assemblyName = System.IO.Path.GetRandomFileName();
            var references = new MetadataReference[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ImmutableArray).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ValueTuple<int, int>).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("netstandard")).Location),
                MetadataReference.CreateFromFile(Assembly.Load(new AssemblyName("System.Runtime")).Location),
            };

            references = references.Concat(CSharpBackend.GetReferencedPaths().Select(p => MetadataReference.CreateFromFile(p))).ToArray();

            CSharpParseOptions options = new CSharpParseOptions(LanguageVersion.Latest);
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] {
                    CSharpSyntaxTree.ParseText(code, options),
                    CSharpSyntaxTree.ParseText(ImplicitlyIncludedCode, options),
                    CSharpSyntaxTree.ParseText(extension, options)
                },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var ms = new System.IO.MemoryStream();
            var result = compilation.Emit(ms);

            // var errors = CSharpScript.Create<int>(code, options: ScriptOptions.Default., assemblyLoader: new InteractiveAssemblyLoader()).Compile();
            Assert.True(result.Success, $"Compilation of generated code failed:\n" + string.Join("\n", result.Diagnostics) + "\n\n" + code);
        }

        public static EmitSettings defaultSettings = new EmitSettings("GeneratedProject.ModelNamespace",
            ImmutableDictionary.CreateRange<string, FullTypeName>(new Dictionary<string,FullTypeName>{
                ["Int"] = new FullTypeName("System.Int32"),
                ["String"] = new FullTypeName("System.String"),
            }),
            validators: ImmutableDictionary<string, ValidatorConfig>.Empty
                        .Add("mySpecialStringValidator", new ValidatorConfig("GeneratedProject.Validators.MySpecialStringValidator", new [] { ("param1", 0, JToken.FromObject(0)) }))
                        .Add("notEmpty", new ValidatorConfig("Coberec.CoreLib.BasicValidators.NotEmpty", null))
                        .Add("notNull", new ValidatorConfig("Coberec.CoreLib.BasicValidators.NotNull", null, acceptsNull: true))
                        .Add("range", new ValidatorConfig("Coberec.CoreLib.BasicValidators.Range", new [] { ("low", 0, (JToken)null), ("high", 1, null) })),
            externalSymbols: new [] {
                new ExternalSymbolConfig("GeneratedProject", "Validators", ExternalSymbolKind.TypeDefinition),
                new ExternalSymbolConfig("GeneratedProject.Validators", "MySpecialStringValidator", ExternalSymbolKind.StaticMethod, typeof(ValidationErrors).FullName, ImmutableArray.Create(
                    new SymbolArgumentConfig("param1", "System.Int32"),
                    new SymbolArgumentConfig("value", "System.String")
                )),
            }
        );
        [Fact]
        public void SimpleCompositeType()
        {
            var schema = new DataSchema(Enumerable.Empty<Entity>(), new [] {
                new TypeDef("Test123", Enumerable.Empty<Directive>(), TypeDefCore.Composite(
                    new [] {
                        new TypeField("Field543", TypeRef.ListType(TypeRef.ActualType("String")), null, Enumerable.Empty<Directive>()),
                        new TypeField("abcSS", TypeRef.ActualType("Int"), null, Enumerable.Empty<Directive>()),
                    },
                    new TypeRef[] {}
                ))
            });

            var result = CSharpBackend.Build(schema, defaultSettings);
            CheckItCompiles(result);
            check.CheckString(result, fileExtension: "cs");
        }

        [Fact]
        public void SimpleUnionType()
        {
            TypeField field543 = new TypeField("Field543", TypeRef.ListType(TypeRef.ActualType("String")), null, new [] { new Directive("validateNotEmpty", JObject.Parse("{}")) });
            var schema = new DataSchema(Enumerable.Empty<Entity>(), new [] {
                new TypeDef("Test123", Enumerable.Empty<Directive>(), TypeDefCore.Composite(
                    new [] {
                        field543,
                        new TypeField("abcSS", TypeRef.ActualType("Int"), null, new [] { new Directive("validateRange", JObject.Parse("{low: 1, high: 10}")) }),
                    },
                    new TypeRef[] { }
                )),
                new TypeDef("Union123", Enumerable.Empty<Directive>(), TypeDefCore.Union(
                    new [] {
                        TypeRef.ActualType("Test123"),
                        TypeRef.ActualType("String"),
                    }))
            });

            var result = CSharpBackend.Build(schema, defaultSettings);
            CheckItCompiles(result);
            check.CheckString(result, fileExtension: "cs");
        }

        [Theory]
        [InlineData("default", true)]
        [InlineData("noOptionalWith", false)]
        public void SimpleInterfaceType(string caseName, bool optionalInterfaceWith)
        {
            TypeField field543 = new TypeField("Field543", TypeRef.ListType(TypeRef.ActualType("String")), null, new [] { new Directive("validateNotEmpty", JObject.Parse("{}")) });
            TypeField field2 = new TypeField("someName", TypeRef.ListType(TypeRef.ActualType("Int")), null, new Directive[0]);
            var schema = new DataSchema(Enumerable.Empty<Entity>(), new [] {
                new TypeDef("Interface1", Enumerable.Empty<Directive>(), TypeDefCore.Interface(
                    new [] {
                        field543,
                        field2
                    }
                )),
                new TypeDef("Test123", Enumerable.Empty<Directive>(), TypeDefCore.Composite(
                    new [] {
                        field543,
                        field2,
                        new TypeField("abcSS", TypeRef.NullableType(TypeRef.ActualType("Int")), null, new [] { new Directive("validateRange", JObject.Parse("{low: 1, high: 10}")) }),
                    },
                    new TypeRef[] { TypeRef.ActualType("Interface1") }
                ))
            });

            var settings = defaultSettings.With(emitOptionalWithMethod: optionalInterfaceWith);

            var result = CSharpBackend.Build(schema, settings);
            CheckItCompiles(result);
            check.CheckString(result, checkName: caseName, fileExtension: "cs");
        }

        [Fact]
        public void CyclicValidatorTest()
        {
            var schema = new DataSchema(Enumerable.Empty<Entity>(), new [] {
                new TypeDef("MyType", new Directive[] {
                    new Directive("validateCustomRule", new JObject()),
                    new Directive("validateCustomRule", new JObject(new JProperty("forFields", new JArray("f4")))),
                }, TypeDefCore.Composite(new [] {
                    new TypeField("f1", TypeRef.NullableType(TypeRef.ActualType("MyType")), null, new Directive[] { new Directive("validateCustomRule", new JObject()) }),
                    new TypeField("f2", TypeRef.NullableType(TypeRef.ActualType("String")), null, new Directive[] { new Directive("validateMySpecialStringValidator", new JObject()) }),
                    new TypeField("f3", TypeRef.ActualType("String"), null, new Directive[] { new Directive("validateMySpecialStringValidator", new JObject(new JProperty("param1", 12))) }),
                    new TypeField("f4", TypeRef.ActualType("MyType"), null, new Directive[] { }),
                }, new TypeRef[0]))
            });
            var settings = defaultSettings.With(
                externalSymbols: defaultSettings.ExternalSymbols.Add(
                    new ExternalSymbolConfig("GeneratedProject.Validators", "CustomValidator", ExternalSymbolKind.StaticMethod, typeof(ValidationErrors).FullName, ImmutableArray.Create(new SymbolArgumentConfig("value", "MyType")))),
                validators: defaultSettings.Validators.Add(
                    "customRule", new ValidatorConfig("GeneratedProject.Validators.CustomValidator", null))
            );

            var result = CSharpBackend.Build(schema, settings);
            CheckItCompiles(result, @"
using System;
using Coberec.CoreLib;
using GeneratedProject.ModelNamespace;

namespace GeneratedProject {
    public static partial class Validators {
        public static ValidationErrors CustomValidator(MyType value) =>
            throw new NotImplementedException();
    }
}
");
            check.CheckString(result, fileExtension: "cs");
        }

        // [Property(MaxTest = 2000, EndSize = 10_000)]
        [Property]
        public void GenerateArbitrarySchema(DataSchema schema)
        {
            // var schema = new DataSchema(Enumerable.Empty<Entity>(), new [] { typeDef });

            var result = CSharpBackend.Build(schema, defaultSettings);
            CheckItCompiles(result);
            // Console.WriteLine(result);
        }

        [Fact]
        public void RoslynCheckWorks()
        {
            CheckItCompiles("using System; namespace NS123 { public class Kokos321 { public int A => 12; } }");
        }

        [Fact]
        public void RoslynCheckImportsCollections()
        {
            CheckItCompiles("using System; using System.Collections.Immutable; namespace NS123 { public class Kokos321 { public ImmutableArray<int> A { get; set; } } }");
        }

        [Fact]
        public void RoslynCheckFailsWhenItShould()
        {
            Assert.ThrowsAny<Exception>(() => {
                CheckItCompiles("using System; using System.Collections.Immutable; namespace NS123 { public class Kokos321 { public ImmutableArray<int> some bullshit code that would not compile A { get; set; } } }");
            });
        }
    }
}