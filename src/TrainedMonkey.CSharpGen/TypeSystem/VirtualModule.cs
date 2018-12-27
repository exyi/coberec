using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Util;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;

namespace Coberec.CSharpGen.TypeSystem
{

    public class VirtualModuleReference : IModuleReference
    {
        private readonly bool isMainModule;
        private readonly string assemblyName;

        public VirtualModuleReference(bool isMainModule, string assemblyName)
        {
            this.isMainModule = isMainModule;
            this.assemblyName = assemblyName;
        }

        private VirtualModule m;
        public VirtualModule Resolve(ICompilation compilation)
        {
            if (m != null) return m;
            return LazyInit.GetOrSet(ref m, new VirtualModule(compilation, assemblyName, this.isMainModule));
        }

        IModule IModuleReference.Resolve(ITypeResolveContext context) => Resolve(context.Compilation);
    }

    public class VirtualModule : IModule
    {
        public PEFile PEFile => null;

        public bool IsMainModule { get; }

        public string AssemblyName { get; }

        public string FullAssemblyName => this.AssemblyName;

        public VirtualNamespace RootNamespace { get; }
        INamespace IModule.RootNamespace => this.RootNamespace;

        public IEnumerable<ITypeDefinition> TopLevelTypeDefinitions => RootNamespace.GetAllTypes(includeNestedTypes: false);

        public IEnumerable<ITypeDefinition> TypeDefinitions => RootNamespace.GetAllTypes(includeNestedTypes: true);

        public SymbolKind SymbolKind => SymbolKind.Module;

        public string Name => this.AssemblyName;

        public ICompilation Compilation { get; }

        public VirtualModule(ICompilation compilation, string assemblyName, bool isMainModule)
        {
            this.Compilation = compilation;
            this.AssemblyName = assemblyName;
            this.IsMainModule = isMainModule;
            this.RootNamespace = new VirtualNamespace("", null, compilation, new[] { this });
        }

        public IEnumerable<IAttribute> GetAssemblyAttributes()
        {
            yield break;
        }

        public IEnumerable<IAttribute> GetModuleAttributes()
        {
            yield break;
        }

        public ITypeDefinition GetTypeDefinition(TopLevelTypeName topLevelTypeName) =>
            this.RootNamespace.FindDescendantNamespace(topLevelTypeName.Namespace)?.GetTypeDefinition(topLevelTypeName.Name, topLevelTypeName.TypeParameterCount);

        public bool InternalsVisibleTo(IModule module) => true;

        public void AddType(ITypeDefinition type)
        {
            var name = type.FullTypeName;
            if (name.NestingLevel != 0) throw new NotSupportedException(); //TODO

            var ns = this.RootNamespace.GetOrAddNamespace(name.TopLevelTypeName.Namespace);
            ns.AddType(name.TopLevelTypeName.Name, name.TopLevelTypeName.TypeParameterCount, type);
        }
    }
}
