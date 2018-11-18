using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using TrainedMonkey.CSharpGen;
using TrainedMonkey.MetaSchema;
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

namespace TrainedMonkey.Tests.CSharp
{
    public class CodeGeneratorTests
    {
        public CodeGeneratorTests()
        {
            Arb.Register(typeof(TestGens.MyArbs));
        }

        void CheckItCompiles(string code)
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

            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees: new[] { CSharpSyntaxTree.ParseText(code, new CSharpParseOptions(LanguageVersion.Latest)) },
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
            }));
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

            var b = new CSharpBackend();

            var result = b.Build(schema, defaultSettings);
            // Console.WriteLine(result);
            CheckItCompiles(result);
        }

        [Fact]
        public void SimpleUnionType()
        {
            var schema = new DataSchema(Enumerable.Empty<Entity>(), new [] {
                new TypeDef("Test123", Enumerable.Empty<Directive>(), TypeDefCore.Composite(
                    new [] {
                        new TypeField("Field543", TypeRef.ListType(TypeRef.ActualType("String")), null, Enumerable.Empty<Directive>()),
                        new TypeField("abcSS", TypeRef.ActualType("Int"), null, Enumerable.Empty<Directive>()),
                    },
                    new TypeRef[] {}
                )),
                new TypeDef("Union123", Enumerable.Empty<Directive>(), TypeDefCore.Union(
                    new [] {
                        TypeRef.ActualType("Test123"),
                        TypeRef.ActualType("String"),
                    }))
            });

            var b = new CSharpBackend();

            var result = b.Build(schema, defaultSettings);
            Console.WriteLine(result);
            CheckItCompiles(result);
        }

        [Property]
        public void GenerateArbitrarySchema(TypeDef typeDef)
        {
            var schema = new DataSchema(Enumerable.Empty<Entity>(), new [] { typeDef });

            var b = new CSharpBackend();

            var result = b.Build(schema, defaultSettings);
            CheckItCompiles(result);
            Console.WriteLine(result);
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
