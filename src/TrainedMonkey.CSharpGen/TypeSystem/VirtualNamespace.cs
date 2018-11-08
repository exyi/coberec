using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;

namespace TrainedMonkey.CSharpGen.TypeSystem
{
    public class VirtualNamespace : INamespace
    {
        private readonly Dictionary<(string name, int typeParameterCount), ITypeDefinition> types;
        private readonly Dictionary<string, VirtualNamespace> namespaces;
        public string ExternAlias => string.Empty;

        public string FullName => ICSharpCode.Decompiler.CSharp.Syntax.NamespaceDeclaration.BuildQualifiedName(ParentNamespace?.FullName, this.Name);

        public string Name { get; }

        public INamespace ParentNamespace { get; }

        public IEnumerable<INamespace> ChildNamespaces => this.namespaces.Values;

        public IEnumerable<ITypeDefinition> Types => this.types.Values;

        public IEnumerable<IModule> ContributingModules { get; }

        public SymbolKind SymbolKind => SymbolKind.Namespace;

        public ICompilation Compilation { get; }

        public VirtualNamespace(string name, INamespace parentNamespace, ICompilation compilation, IModule[] modules)
        {
            this.Compilation = compilation;
            this.types = new Dictionary<(string name, int typeParameterCount), ITypeDefinition>(new TupleComparer<string, int>(compilation.NameComparer, EqualityComparer<int>.Default));
            this.namespaces = new Dictionary<string, VirtualNamespace>(compilation.NameComparer);
            this.ContributingModules = modules;
            this.ParentNamespace = parentNamespace;
            this.Name = name;
        }

        internal IEnumerable<ITypeDefinition> GetAllTypes(bool includeNestedTypes)
        {
            return
                this.Types
                .Concat(includeNestedTypes ? this.Types.SelectMany(t => t.NestedTypes) : Enumerable.Empty<ITypeDefinition>())
                .Concat(this.namespaces.Values.SelectMany(n => n.GetAllTypes(includeNestedTypes)));
        }

        public INamespace GetChildNamespace(string name) => this.namespaces.TryGetValue(name, out var r) ? r : null;

        public ITypeDefinition GetTypeDefinition(string name, int typeParameterCount) => name != null && this.types.TryGetValue((name, typeParameterCount), out var r) ? r : null;

        public VirtualNamespace GetOrAddNamespace(string name)
        {
            if (name.Contains("."))
            {
                var s = name.Split(new[] { '.' }, 2);
                return GetOrAddNamespace(s[0]).GetOrAddNamespace(s[1]);
            }
            else
            {
                if (this.namespaces.TryGetValue(name, out var result))
                    return result;
                else
                    return this.namespaces[name] = new VirtualNamespace(name, this, this.Compilation, this.ContributingModules.ToArray());
            }
        }

        public void AddType(string name, int typeParameterCount, ITypeDefinition type)
        {
            this.types.Add((name, typeParameterCount), type);
        }
    }
}
