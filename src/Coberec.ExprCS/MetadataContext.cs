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
    /// <summary>
    /// Hold the entire compilation.
    ///</summary>
    public class MetadataContext
    {
        private readonly HackedSimpleCompilation hackedCompilation;
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
        public ImmutableArray<ModuleSignature> Modules { get; }
        public ModuleSignature MainModule { get; }
        internal IModule MainILSpyModule => mutableModule;
        readonly VirtualModule mutableModule;
        private readonly Dictionary<ModuleSignature, IModule> moduleMap;
        private readonly Dictionary<MemberSignature, IEntity> declaredEntities = new Dictionary<MemberSignature, IEntity>();
        public IReadOnlyDictionary<MemberSignature, IEntity> DeclaredEntities => declaredEntities;
        private Dictionary<TypeSignature, TypeDef> definedTypes = new Dictionary<TypeSignature, TypeDef>();
        public IReadOnlyCollection<TypeDef> DefinedTypes => definedTypes.Values;
        private Dictionary<FullTypeName, TypeDef> definedTypeNames = new Dictionary<FullTypeName, TypeDef>();

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

        internal IModule GetModule(ModuleSignature module) =>
            moduleMap.TryGetValue(module, out var result) ? result :
            throw new ArgumentException($"Module {module} is not known.");

        internal INamespace GetNamespace(NamespaceSignature ns) =>
            ns == NamespaceSignature.Global ? Compilation.RootNamespace :
            GetNamespace(ns.Parent).GetChildNamespace(ns.Name) ?? throw new Exception($"Could not resolve namespace {ns}.");

        internal ITypeDefinition GetTypeDef(TypeSignature type) =>
            declaredEntities.TryGetValue(type, out var result) ? (ITypeDefinition)result :
            type.Parent.Match(
                ns => GetNamespace(ns.Item).GetTypeDefinition(type.Name, type.GenericParamCount) ?? throw new InvalidOperationException($"Type {type.GetFullTypeName()} could not be found"),
                parentType => GetTypeDef(parentType.Item).GetNestedTypes(t => t.Name == type.Name).Single().GetDefinition());

        public TypeSignature FindTypeDef(string name)
        {
            var fullTypeName = new FullTypeName(name);
            if (this.definedTypeNames.TryGetValue(fullTypeName, out var result))
                return result.Signature;
            else
                return SymbolLoader.Type(Compilation.FindType(fullTypeName).GetDefinition());
        }
        public TypeSignature FindTypeDef(Type type) =>
            SymbolLoader.Type(Compilation.FindType(type).GetDefinition());


        public TypeReference FindType(string name)
        {
            var fullTypeName = new FullTypeName(name);
            if (this.definedTypeNames.TryGetValue(fullTypeName, out var result))
                return result.Signature;
            else
                return SymbolLoader.TypeRef(Compilation.FindType(fullTypeName));
        }
        public TypeReference FindType(Type type) =>
            SymbolLoader.TypeRef(Compilation.FindType(type));

        public IEnumerable<TypeSignature> GetTopLevelTypes() =>
            Compilation.GetTopLevelTypeDefinitions().Where(t => !(t is VirtualType)).Select(SymbolLoader.Type)
            .Concat(this.definedTypes.Keys);

        public IEnumerable<TypeSignature> GetTopLevelTypes(ModuleSignature module) =>
            GetModule(module).TopLevelTypeDefinitions.Select(SymbolLoader.Type);

        public IEnumerable<TypeOrNamespace> GetNamespaceMembers(NamespaceSignature @namespace)
        {
            var ns = GetNamespace(@namespace);
            return ns.ChildNamespaces.Select(n => SymbolLoader.Namespace(n.FullName)).Select(TypeOrNamespace.NamespaceSignature)
                   .Concat(ns.Types.Select(SymbolLoader.Type).Select(TypeOrNamespace.TypeSignature));
        }

        public IEnumerable<MethodSignature> GetMemberMethods(TypeSignature type) =>
            GetTypeDef(type).GetMethods(null, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Method);

        public IEnumerable<FieldSignature> GetMemberFields(TypeSignature type) =>
            GetTypeDef(type).GetFields(null, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Field);

        public IEnumerable<PropertySignature> GetMemberProperties(TypeSignature type) =>
            GetTypeDef(type).GetProperties(null, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Property);

        public IEnumerable<SpecializedType> GetBaseTypes(TypeSignature type) =>
            GetTypeDef(type).GetAllBaseTypes().Select(SymbolLoader.TypeRef).Select(t => Assert.IsType<TypeReference.SpecializedTypeCase>(t).Item);

        public IEnumerable<SpecializedType> GetDirectImplements(TypeSignature type) =>
            GetTypeDef(type).DirectBaseTypes.Where(b => b.Kind == TypeKind.Interface).Select(SymbolLoader.TypeRef).Select(t => Assert.IsType<TypeReference.SpecializedTypeCase>(t).Item);

        public IEnumerable<MemberSignature> GetMembers(TypeSignature type) =>
            GetMemberMethods(type).AsEnumerable<MemberSignature>()
            .Concat(GetMemberProperties(type))
            .Concat(GetMemberFields(type));


        private Dictionary<TypeSignature, List<ILSpyArbitraryTypeModification>> typeMods = new Dictionary<TypeSignature, List<ILSpyArbitraryTypeModification>>();

        public EmitSettings Settings { get; }

        public void RegisterTypeMod(TypeSignature type, Action<VirtualType> declareMembers, Action<VirtualType> completeDefinitions = null) =>
            RegisterTypeMod(type, new ILSpyArbitraryTypeModification(declareMembers, completeDefinitions));
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

        public void AddType(TypeDef type)
        {
            var xx = MetadataDefiner.CreateTypeDefinition(this, type);
            mutableModule.AddType(xx);
            MetadataDefiner.DefineTypeMembers(xx, this, type);
        }

// #if ExposeILSpy
        public TypeSignature AddRawType(ITypeDefinition type)
        {
            mutableModule.AddType(type);
            return SymbolLoader.Type(type);
        }
// #endif

        private CSharpEmitter BuildCore()
        {
            var s = new DecompilerSettings(LanguageVersion.Latest);
            s.CSharpFormattingOptions.AutoPropertyFormatting = PropertyFormatting.ForceOneLine;
            s.CSharpFormattingOptions.PropertyBraceStyle = BraceStyle.DoNotChange;

            var emitter = new CSharpEmitter(this.hackedCompilation, s, this.Settings.EmitPartialClasses);
            return emitter;
        }

        public string EmitToString()
        {
            var e = BuildCore();
            return e.DecompileWholeModuleAsString();
        }

        public IEnumerable<string> EmitToDirectory(string targetDir)
        {
            var e = BuildCore();
            return e.WriteCodeFilesInProject(targetDir);
        }

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
