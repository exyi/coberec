﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis;
using Coberec.CSharpGen;
using IO = System.IO;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Linq;
using Coberec.CoreLib;
using GLoader = Coberec.GraphqlLoader.GraphqlLoader;

namespace Coberec.CLI
{
    public class CompileOptions
    {
        public List<string> Inputs = new List<string>();
        public string ConfigurationFile;
        public string Output;
        public bool OutputDirectory;
        public string OutputNamespace;
        public bool Verbose;
        public bool InvertNonNullable;
    }
    public static class Program
    {
        static async Task<string> ReadFile(string path)
        {
            using (var rdr = new StreamReader(path))
            {
                return await rdr.ReadToEndAsync();
            }
        }
        static (string name, Lazy<string> content) GetInput(string inputFile)
        {
            if (inputFile == "-")
            {
                Console.Error.WriteLine("Reading input GraphQL Schema from standard input.");
                return ("stdin.gql", new Lazy<string>(() => Console.In.ReadToEnd()));
            }
            else
            {
                var f = ReadFile(inputFile);
                return (inputFile, new Lazy<string>(() => f.GetAwaiter().GetResult()));
            }
        }
        public static void Compile(CompileOptions x)
        {
            EmitSettings settings =
                x.ConfigurationFile != null ?
                JsonConvert.DeserializeObject<EmitSettings>(File.ReadAllText(x.ConfigurationFile)) :
                new EmitSettings("");
            settings = settings.With(
                @namespace: x.OutputNamespace ?? settings.Namespace,
                primitiveTypeMapping:
                    settings.PrimitiveTypeMapping
                    .TryAdd("Int", "System.Int32")
                    .TryAdd("String", "System.String")
                    .TryAdd("ID", "System.Guid")
                    .TryAdd("Float", "System.Double")
                    .TryAdd("Boolean", "System.Boolean"),
                validators: settings.Validators
                    .TryAdd("notEmpty", new ValidatorConfig("Coberec.CoreLib.BasicValidators.NotEmpty", null))
                    .TryAdd("notNull", new ValidatorConfig("Coberec.CoreLib.BasicValidators.NotNull", null, acceptsNull: true))
                    .TryAdd("range", new ValidatorConfig("Coberec.CoreLib.BasicValidators.Range", new [] { ("low", 0, (JToken)null), ("high", 1, null) }))
            );

            var (schema, validationMapper) = GLoader.LoadFromGraphQL(x.Inputs.Select(GetInput).ToArray(), x.InvertNonNullable);

            try
            {
                if (x.OutputDirectory)
                {
                    foreach (var f in Directory.EnumerateFiles(x.Output, "*.cs", SearchOption.AllDirectories))
                        File.Delete(f);
                    Directory.CreateDirectory(x.Output);
                    CSharpBackend.BuildIntoFolder(schema, settings, x.Output);
                }
                else
                {
                    var output = CSharpBackend.Build(schema, settings);
                    if (x.Output == "-")
                        Console.Out.WriteLine(output);
                    else
                        File.WriteAllText(x.Output, output);
                }
            }
            catch (ValidationErrorException error)
            {
                throw new AggregateException(GLoader.MapErrors(error.Validation, validationMapper));
            }
        }

        public static CompileOptions ParseOptions(string[] args)
        {
            var x = new CompileOptions();
            for (int index = 0; args.Length > index; index++)
            {
                var a = args[index];
                string nextArg()
                {
                    if (index + 1 >= args.Length) throw new Exception($"Argument {a} must be followed by a next one.");
                    return args[index + 1];
                }
                if (a == "--config")
                {
                    x.ConfigurationFile = nextArg();
                    index++;
                }
                else if (a == "--out")
                {
                    x.Output = nextArg();
                    x.OutputDirectory = false;
                    index++;
                }
                else if (a == "--outDir")
                {
                    x.Output = nextArg();
                    x.OutputDirectory = true;
                    index++;
                }
                else if (a == "--namespace")
                {
                    x.OutputNamespace = nextArg();
                    index++;
                }
                else if (a == "--input")
                {
                    x.Inputs.Add(nextArg());
                    index++;
                }
                else if (a == "--verbose" || a == "-v")
                {
                    x.Verbose = true;
                }
                else if (a == "--invertNonNullable")
                {
                    x.InvertNonNullable = true;
                }
                else if (a.StartsWith("-"))
                {
                    throw new Exception($"Unknown parameter {a}.");
                }
                else
                {
                    x.Inputs.Add(a);
                }
            }
            if (x.Output is null)
                throw new Exception($"Output must be specified (either `--out` or `--outDir` option). Use `--output -` to use standard output.");
            if (x.ConfigurationFile is null && x.OutputNamespace is null)
                throw new Exception($"Output namespace must be specified (either by `--namespace` option or through config file in the `--config` option).");
            if (x.Inputs.Count == 0)
                throw new Exception($"Some input must be specified. Use `-` to use standard input.");
            return x;
        }

        public const string Help = @"
Translates GraphQL Schema into C# immutable classes

Usage: Coberec.CLI.exe input1.gql ... input43.gql --outDir ./GeneratedClasses --config coberec.config.json --namespace MyProject.Model

Parameters:

--config configFile.json:
    Json file used for configuration of code generator, see config docs for more info. Supports most JSON5 features (unquoted identifiers, trailing commas, C-style comments).

--out outFile.cs:
    Generated code goes into the specified file. You can use `-` to write to std out.

--outDir outDirectory:
    Generated code goes into the specified directory. Usually, 1 class goes into 1 file (except for name conflict and nested classes).

--namespace MyNamespace:
    Specifies C# namespace of the generated classes. The same option can be set in the configuration file, but this one has precedence.

[--input] inputFile.gql:
    Add the specified input file into the schema.

--verbose:
    Prints a bit more information sometimes.

--invertNonNullable
    Makes non-nullable types nullable and vice versa. It's useful hack for the case when almost everything is non-nullable as it's the default with this option.
";

        public static int Main(string[] args)
        {
            // Console.Write("attach debugger...");
            // while (!System.Diagnostics.Debugger.IsAttached) System.Threading.Thread.Sleep(200);
            // Console.WriteLine("  OK");
            if (args.Contains("--help"))
            {
                Console.Error.WriteLine(Help);
            }
            try
            {
                var a = ParseOptions(args);
                Compile(a);
                return 0;
            }
            catch (Exception error)
            {
                var message = error is AggregateException multiError ? "Multiple errors occurred:\n * " + string.Join("\n * ", multiError.InnerExceptions.Select(a => a.Message)) : error.Message;
                var verbose = args.Contains("--verbose") || args.Contains("-v");
                if (verbose)
                {
                    Console.Error.Write("Error has occurred: ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(error.Message);
                    Console.ResetColor();
                    Console.Error.WriteLine(error);
                    Console.Error.WriteLine($"If this error does not make sense, it's probably a bug. Please, try to create a small reproducible example and report it.");
                }
                else
                {
                    Console.Error.Write($"Error: ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.Write(error.Message);
                    Console.ResetColor();
                    Console.Error.WriteLine("For more information (exception stack trace) use --verbose.");
                }
                return 1;
            }

            // var input = Console.In.ReadToEnd();

            // var schema = Coberec.GraphqlLoader.GraphqlLoader.LoadFromGraphQL(new [] { ("stdin.gql", new Lazy<string>(input)) });
            // var settings = new EmitSettings("GeneratedProject.ModelNamespace",
            //     ImmutableDictionary.CreateRange<string, FullTypeName>(new Dictionary<string,FullTypeName>{
            //         ["Int"] = new FullTypeName("System.Int32"),
            //         ["String"] = new FullTypeName("System.String"),
            //         ["ID"] = new FullTypeName("System.Guid"),
            //         ["Float"] = new FullTypeName("System.Double"),
            //         ["Boolean"] = new FullTypeName("System.Boolean"),
            //     }));
            // var result = CSharpBackend.Build(schema, settings);
            // Console.WriteLine(result);

            // Emit(args[0], args[1]).Wait();
        }
        // public static async Task Emit(string projectPath, string outPath)
        // {
        //     // new Microsoft.CodeAnalysis.MSBuild.MSBuildProjectLoader().LoadProjectInfoAsync()
        //     var w = MSBuildWorkspace.Create();
        //     var s = await w.OpenSolutionAsync(projectPath);
        //     var p = s.Projects.First(p_ => p_.Name.Contains("CLI"));
        //     var c = await p.GetCompilationAsync();

        //     using (var dllFile = IO.File.Create(IO.Path.Combine(outPath, c.AssemblyName + ".dll")))
        //     using (var pdbFile = IO.File.Create(IO.Path.Combine(outPath, c.AssemblyName + ".dll.pdb")))
        //     {
        //         var result = c.Emit(dllFile, pdbFile);
        //         if (!result.Success)
        //             Console.WriteLine("Some error:");
        //         foreach(var d in result.Diagnostics)
        //             Console.WriteLine(d);
        //     }
        // }
    }
}
