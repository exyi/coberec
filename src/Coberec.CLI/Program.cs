using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Coberec.CSharpGen;
using IO = System.IO;

namespace Coberec.CLI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var input = Console.In.ReadToEnd();

            var schema = Coberec.GraphqlLoader.GraphqlLoader.LoadFromGraphQL(new [] { ("stdin.gql", new Lazy<string>(input)) });
            var settings = new EmitSettings("GeneratedProject.ModelNamespace",
                ImmutableDictionary.CreateRange<string, FullTypeName>(new Dictionary<string,FullTypeName>{
                    ["Int"] = new FullTypeName("System.Int32"),
                    ["String"] = new FullTypeName("System.String"),
                    ["ID"] = new FullTypeName("System.Guid"),
                    ["Float"] = new FullTypeName("System.Double"),
                    ["Boolean"] = new FullTypeName("System.Boolean"),
                }));
            var result = CSharpBackend.Build(schema, settings);
            Console.WriteLine(result);

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
