using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using TS=ICSharpCode.Decompiler.TypeSystem;
using Coberec.CSharpGen.TypeSystem;
using System.Runtime.Loader;
using ICSharpCode.Decompiler.Metadata;
using System.Reflection.PortableExecutable;
using System.Collections.Concurrent;
using System.Diagnostics;
using Coberec.CSharpGen.Emit;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using Coberec.CSharpGen;
using ICSharpCode.Decompiler.CSharp.Resolver;
using Xunit;

namespace Coberec.ExprCS
{
    /// Holds all the information needed to generate the C# code
    public class MetadataContext
    {
        private readonly HackedSimpleCompilation hackedCompilation;
        /// <summary> ILSpy's ICompilation object </summary>
#if ExposeILSpy
        public
#else
        internal
#endif
        ICompilation Compilation => hackedCompilation;

        internal CSharpConversions CSharpConversions { get; }


        internal MetadataContext(HackedSimpleCompilation compilation, EmitSettings settings)
        {
            this.hackedCompilation = compilation;
            Settings = settings ?? new EmitSettings();
            CSharpConversions = CSharpConversions.Get(compilation);
            moduleMap = compilation.Modules.ToDictionary(m => new ModuleSignature(m.Name));
            Modules = moduleMap.Keys.ToImmutableArray();
            MainModule = Modules[0];
            mutableModule = (VirtualModule)moduleMap[MainModule];

            Debug.Assert(MainModule.Name == this.Compilation.MainModule.Name);
        }

        /// <summary> Creates new empty context. </summary>
        /// <param name="references"> A list of assembly paths that will be included in the compilation. </param>
        public static MetadataContext Create(
            string mainModuleName,
            IEnumerable<string> references = null,
            EmitSettings settings = null)
        {
            var referencedModules =
                references == null ? ReferencedModules.Value :
                ReferencedModules.Value.Concat(references.Distinct().Select(r => new PEFile(r, PEStreamOptions.PrefetchMetadata)).ToArray());
            // TODO   ^ add a way to override this implicit reference

            var compilation = new HackedSimpleCompilation(
                new VirtualModuleReference(true, mainModuleName),
                referencedModules
            );

            return new MetadataContext(compilation, settings);
        }
        /// <summary> All .NET modules loaded in the compilation. </summary>
        public ImmutableArray<ModuleSignature> Modules { get; }
        /// <summary> The .NET module which contains the code that is being generated. </summary>
        public ModuleSignature MainModule { get; }
        /// <summary> The main module from ILSpy's type system which contains the code that is being generated. </summary>
        internal IModule MainILSpyModule => mutableModule;
        readonly VirtualModule mutableModule;
        private readonly Dictionary<ModuleSignature, IModule> moduleMap;
        private readonly Dictionary<MemberSignature, IEntity> declaredEntities = new Dictionary<MemberSignature, IEntity>();
        /// <summary> A dictionary of all entities (type, methods, fields, ...) that have declaring so far. Maps from ExprCS's signature into ILSpy's entities. </summary>
        public IReadOnlyDictionary<MemberSignature, IEntity> DeclaredEntities => declaredEntities;
        private Dictionary<TypeSignature, TypeDef> definedTypes = new Dictionary<TypeSignature, TypeDef>();
        public IReadOnlyCollection<TypeDef> DefinedTypes => definedTypes.Values;
        private Dictionary<FullTypeName, TypeDef> definedTypeNames = new Dictionary<FullTypeName, TypeDef>();

        /// <summary> Adds a new entity into <see cref="DeclaredEntities" />. </summary>
        internal void RegisterEntity(MemberDef member, IEntity entity)
        {
            declaredEntities.Add(member.Signature, entity);
            if (member is TypeDef type)
            {
                this.definedTypes.Add(type.Signature, type);
                this.definedTypeNames.Add(type.Signature.GetFullTypeName(), type);
            }
            SymbolLoader.RegisterDeclaredEntity(entity, member.Signature);
        }

        /// <summary> Finds ILSpy's IModule based on ExprCS ModuleSignature. The IModule has access to the module contents, so it's much stronger than ModuleSignature. </summary>
        internal IModule GetModule(ModuleSignature module) =>
            moduleMap.TryGetValue(module, out var result) ? result :
            throw new ArgumentException($"Module {module} is not known.");

        /// <summary> Finds ILSpy's INamespace based on ExprCS NamespaceSignature. The INamespace has access to the module contents, so it's much stronger than NamespaceSignature. </summary>
        internal INamespace GetNamespace(NamespaceSignature ns) =>
            ns == NamespaceSignature.Global ? Compilation.RootNamespace :
            GetNamespace(ns.Parent).GetChildNamespace(ns.Name) ?? throw new Exception($"Could not resolve namespace {ns}.");

        /// <summary> Finds ILSpy's ITypeDefinition based on ExprCS TypeSignature. The ITypeDefinition has access to the module contents, so it's much stronger than TypeSignature. </summary>
        internal ITypeDefinition GetTypeDef(TypeSignature type) =>
            declaredEntities.TryGetValue(type, out var result) ? (ITypeDefinition)result :
            type.Parent.Match(
                ns => GetNamespace(ns.Item).GetTypeDefinition(type.Name, type.GenericParamCount) ?? throw new InvalidOperationException($"Type {type.GetFullTypeName()} could not be found"),
                parentType => GetTypeDef(parentType.Item).GetNestedTypes(t => t.Name == type.Name).Single().GetDefinition());

        /// <summary> Finds a type signature by a <paramref name="name" />. Will only look for type definitions (e.g. <see cref="String" />), not type references (<see cref="String[]" /> or <see cref="List{String}" />). </summary>
        public TypeSignature FindTypeDef(string name)
        {
            var fullTypeName = new FullTypeName(name);
            if (this.definedTypeNames.TryGetValue(fullTypeName, out var result))
                return result.Signature;
            else
                return SymbolLoader.Type(Compilation.FindType(fullTypeName).GetDefinition());
        }

        [Obsolete]
        public TypeSignature FindTypeDef(Type type) =>
            SymbolLoader.Type(Compilation.FindType(type).GetDefinition());


        /// <summary> Finds a type reference by a <paramref name="name" />. Will look for both type definitions (e.g. <see cref="String" />) and for type references (<see cref="String[]" /> or <see cref="List{String}" />). </summary>
        public TypeReference FindType(string name)
        {
            var fullTypeName = new FullTypeName(name);
            if (this.definedTypeNames.TryGetValue(fullTypeName, out var result))
                return result.Signature;
            else
                return SymbolLoader.TypeRef(Compilation.FindType(fullTypeName));
        }
        [Obsolete]
        public TypeReference FindType(Type type) =>
            SymbolLoader.TypeRef(Compilation.FindType(type));

        /// <summary> Lists all top level types (i.e. not nested types), both the declared ones and from dependencies. </summary>
        public IEnumerable<TypeSignature> GetTopLevelTypes() =>
            Compilation.GetTopLevelTypeDefinitions().Where(t => !(t is VirtualType)).Select(SymbolLoader.Type)
            .Concat(this.definedTypes.Keys);

        /// <summary> Lists all top level types (i.e. not nested types) from the module. </summary>
        public IEnumerable<TypeSignature> GetTopLevelTypes(ModuleSignature module) =>
            GetModule(module).TopLevelTypeDefinitions.Select(SymbolLoader.Type);

        /// <summary> Lists all types and namespaces that are directly in the <paramref name="namespace" /> (i.e. they are not nested) </summary>
        public IEnumerable<TypeOrNamespace> GetNamespaceMembers(NamespaceSignature @namespace)
        {
            var ns = GetNamespace(@namespace);
            return ns.ChildNamespaces.Select(n => SymbolLoader.Namespace(n.FullName)).Select(TypeOrNamespace.NamespaceSignature)
                   .Concat(ns.Types.Select(SymbolLoader.Type).Select(TypeOrNamespace.TypeSignature));
        }

        /// <summary> Lists all methods of the specified <paramref name="type" />. Only includes the methods declared in this type, not the inherited ones. </summary>
        public IEnumerable<MethodSignature> GetMemberMethods(TypeSignature type) =>
            GetTypeDef(type).GetMethods(null, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Method);

        /// <summary> Lists all fields of the specified <paramref name="type" />. Only includes the fields declared in this type, not the inherited ones. </summary>
        public IEnumerable<FieldSignature> GetMemberFields(TypeSignature type) =>
            GetTypeDef(type).GetFields(null, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Field);

        /// <summary> Lists all properties of the specified <paramref name="type" />. Only includes the properties declared in this type, not the inherited ones. </summary>
        public IEnumerable<PropertySignature> GetMemberProperties(TypeSignature type) =>
            GetTypeDef(type).GetProperties(null, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Property);

        /// <summary> Gets all base types of the specified <paramref name="type" />. </summary>
        public IEnumerable<SpecializedType> GetBaseTypes(TypeSignature type) =>
            GetTypeDef(type).GetAllBaseTypes().Select(SymbolLoader.TypeRef).Select(t => Assert.IsType<TypeReference.SpecializedTypeCase>(t).Item);

        /// <summary> Gets all implemented interfaces of this specific <paramref name="type" />. Does not include the transitively implemented ones from its base types. </summary>
        public IEnumerable<SpecializedType> GetDirectImplements(TypeSignature type) =>
            GetTypeDef(type).DirectBaseTypes.Where(b => b.Kind == TypeKind.Interface).Select(SymbolLoader.TypeRef).Select(t => Assert.IsType<TypeReference.SpecializedTypeCase>(t).Item);

        /// <summary> Lists all members of the specified <paramref name="type" />. Only includes the members declared in this type, not the inherited ones. </summary>
        public IEnumerable<MemberSignature> GetMembers(TypeSignature type) =>
            GetMemberMethods(type).AsEnumerable<MemberSignature>()
            .Concat(GetMemberProperties(type))
            .Concat(GetMemberFields(type));


        private Dictionary<TypeSignature, List<ILSpyArbitraryTypeModification>> typeMods = new Dictionary<TypeSignature, List<ILSpyArbitraryTypeModification>>();

        /// <summary> The settings that controls how the code is generated set in <see cref="Create(string, IEnumerable{string}, EmitSettings)" />. Note that you can only read this property, if you'd like to change it, use the last parameter of the Create method. </summary>
        public EmitSettings Settings { get; }

        /// <summary> Registers a functions that can modify the ILSpy type definition. This may be useful if a feature is missing from ExprCS. </summary>
        public void RegisterTypeMod(TypeSignature type, Action<VirtualType> declareMembers, Action<VirtualType> completeDefinitions = null) =>
            RegisterTypeMod(type, new ILSpyArbitraryTypeModification(declareMembers, completeDefinitions));
        /// <summary> Registers an object with functions that can modify the ILSpy type definition. This may be useful if a feature is missing from ExprCS. </summary>
        public void RegisterTypeMod(TypeSignature type, ILSpyArbitraryTypeModification modification)
        {
            if (definedTypes.ContainsKey(type))
                throw new Exception($"Type {type.GetFullTypeName()} was already defined.");

            if (!typeMods.TryGetValue(type, out var list))
                typeMods[type] = list = new List<ILSpyArbitraryTypeModification>();

            list.Add(modification);
        }

        internal IEnumerable<ILSpyArbitraryTypeModification> GetTypeMods(TypeSignature type) =>
            typeMods.GetValueOrDefault(type)?.AsEnumerable() ?? Enumerable.Empty<ILSpyArbitraryTypeModification>();

        private List<(TypeDef type, Action<Exception> errorHandler)> waitingTypes = new List<(TypeDef type, Action<Exception> errorHandler)>();

        /// <summary> Adds a top level type definition that will be included in the generated code. </summary>
        /// <param name="errorHandler">The error handler will be invoked when an exception occurs during creating of this type. If not null, it will always catch the exception, so you may want to rethrow it with some more information about the generated type.</param>
        public void AddType(TypeDef type, Action<Exception> errorHandler = null)
        {
            waitingTypes.Add((type, errorHandler));
        }

        /// <summary> Generates the "real" types from the symbolic <see cref="TypeDef" />s. When this method is called, all symbols used in the metadata must be already added to the context (using <see cref="AddType(TypeDef, Action{Exception})" />) </summary>
        public void CommitWaitingTypes()
        {
            var sortedTypes = MetadataDefiner.SortDefinitions(this.waitingTypes.Select(t => t.type).ToArray());
            var types =
                sortedTypes.Select(t => {
                    var vtype = MetadataDefiner.CreateTypeDefinition(this, t);
                    mutableModule.AddType(vtype);
                    return vtype;
                }).ToArray();
            var handlers = this.waitingTypes.ToDictionary(t => t.type.Signature, t => t.errorHandler);
            foreach (var (type, vtype) in sortedTypes.ZipTuples(types))
            {
                var errorHandler = handlers[type.Signature];
                try
                {
                    MetadataDefiner.DefineTypeMembers(vtype, this, type);
                }
                catch (Exception ex) when (errorHandler != null)
                {
                    errorHandler(ex);
                }
            }
            this.waitingTypes.Clear();
        }

// #if ExposeILSpy
        /// <summary> Adds an ILSpy's representation of top level type definition that will be included in the generated code. You may want to use <see cref="VirtualType" /> if you want to create the type yourself or <see cref="ICSharpCode.Decompiler.TypeSystem.Implementation.MetadataTypeDefinition" /> if you want to use a type from a compiled .NET module. </summary>
        public TypeSignature AddRawType(ITypeDefinition type)
        {
            mutableModule.AddType(type);
            return SymbolLoader.Type(type);
        }
// #endif

        private CSharpEmitter BuildCore()
        {
            CommitWaitingTypes();
            var s = new DecompilerSettings(LanguageVersion.Latest);
            s.CSharpFormattingOptions.AutoPropertyFormatting = PropertyFormatting.ForceOneLine;
            s.CSharpFormattingOptions.PropertyBraceStyle = BraceStyle.DoNotChange;

            var emitter = new CSharpEmitter(this.hackedCompilation, s, this.Settings.EmitPartialClasses);
            return emitter;
        }

        /// <summary> Generates all the code and returns it as one string (that is expected to be in one file) </summary>
        public string EmitToString()
        {
            var e = BuildCore();
            return e.DecompileWholeModuleAsString();
        }

        /// <summary> Generates all the code and writes it to the specified directory - one top-level type per one file. </summary>
        /// <returns> The list of all file names that were emitted. </returns>
        public IEnumerable<string> EmitToDirectory(string targetDir)
        {
            var e = BuildCore();
            return e.WriteCodeFilesInProject(targetDir);
        }

        /// <summary> Gets a list of referenced paths by this program. </summary>
        public static IEnumerable<string> GetReferencedPaths() =>
            from r in Enumerable.Concat(typeof(MetadataContext).Assembly.GetReferencedAssemblies(), new[] {
                typeof(string).Assembly.GetName(),
                typeof(System.Collections.StructuralComparisons).Assembly.GetName(),
                typeof(ValueTuple<int, int>).Assembly.GetName(),
                typeof(Uri).Assembly.GetName()
                // new AssemblyName("netstandard")
            })
            let location = AssemblyLoadContext.Default.LoadFromAssemblyName(r).Location
            where !string.IsNullOrEmpty(location)
            let lUrl = new Uri(location)
            select lUrl.AbsolutePath;

        private static Lazy<PEFile[]> ReferencedModules = new Lazy<PEFile[]>(() => GetReferencedPaths().Distinct().Select(a => new PEFile(a, System.Reflection.PortableExecutable.PEStreamOptions.PrefetchMetadata)).ToArray());
    }
}
