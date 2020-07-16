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
using ICSharpCode.Decompiler.Documentation;

namespace Coberec.ExprCS
{
    /// <summary> Holds all the information needed to generate the C# code </summary>
    public sealed class MetadataContext
    {
        private readonly HackedSimpleCompilation hackedCompilation;
        /// <summary> ILSpy expose: ILSpy's ICompilation object </summary>
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
        /// <param name="references"> A list of assembly paths that will be included in the compilation. When null a default list is used (that contains some standard libraries) </param>
        public static MetadataContext Create(
            IEnumerable<string> references = null,
            EmitSettings settings = null)
        {
            var modules = new HashSet<string>();
            var referencedModules =
                references == null ? ReferencedModules.Value :
                references.Distinct().Select(r => new PEFile(r, PEStreamOptions.PrefetchMetadata)).Concat(ReferencedModules.Value)
                .Where(m => modules.Add(m.Name))
                .ToArray();
            // TODO   ^ add a way to override this implicit reference

            var compilation = new HackedSimpleCompilation(
                new VirtualModuleReference(true, "ExprCS.MainModule"),
                referencedModules
            );

            return new MetadataContext(compilation, settings);
        }
        /// <summary> All .NET modules loaded in the compilation. </summary>
        public ImmutableArray<ModuleSignature> Modules { get; }
        /// <summary> The .NET module which contains the code that is being generated. </summary>
        public ModuleSignature MainModule { get; }
        /// <summary> ILSpy expose: The main module from ILSpy's type system which contains the code that is being generated. </summary>
        internal IModule MainILSpyModule => mutableModule;
        readonly VirtualModule mutableModule;
        private readonly Dictionary<ModuleSignature, IModule> moduleMap;
        private readonly Dictionary<MemberSignature, IEntity> declaredEntities = new Dictionary<MemberSignature, IEntity>();
        /// <summary> ILSpy expose: A dictionary of all entities (type, methods, fields, ...) that have declaring so far. Maps from ExprCS's signature into ILSpy's entities. </summary>
        public IReadOnlyDictionary<MemberSignature, IEntity> DeclaredEntities => declaredEntities;
        private Dictionary<TypeSignature, TypeDef> definedTypes = new Dictionary<TypeSignature, TypeDef>();
        public IReadOnlyCollection<TypeDef> DefinedTypes => definedTypes.Values;
        private Dictionary<FullTypeName, TypeDef> definedTypeNames = new Dictionary<FullTypeName, TypeDef>();

        internal GenericParameterStore GenericParameterStore { get; } = new GenericParameterStore();

        /// <summary> Adds a new entity into <see cref="DeclaredEntities" />. </summary>
        internal void RegisterEntity(MemberDef member, IEntity entity)
        {
            declaredEntities.Add(member.Signature, entity);
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
        public ITypeDefinition GetTypeDef(TypeSignature type) =>
            (ITypeDefinition)declaredEntities.GetValueOrDefault(type) ??
           (definedTypes.ContainsKey(type) ? throw new Exception($"Type {type} has been added to MetadataContext, but it hasn't been comited, so the ILSpy metadata can not be obtained.") :
            type.Parent.Match(
                ns => {
                    var ilspyNs = GetNamespace(ns);
                    var foundType = ilspyNs.GetTypeDefinition(type.Name, type.TypeParameters.Length) ??
                        throw new InvalidOperationException($"Type {type.GetFullTypeName()} could not be found, namespace {ns} does not contain such type.");
                    Assert.Equal(type, SymbolLoader.Type(foundType));
                    return foundType;
                },
                parentType => {
                    var parent = GetTypeDef(parentType);
                    return parent.GetNestedTypes(t => t.Name == type.Name && t.TypeParameterCount - parent.TypeParameterCount == type.TypeParameters.Length, GetMemberOptions.IgnoreInheritedMembers)
                                 .SingleOrDefault()
                                 ?.GetDefinition() ?? throw new Exception($"Nested type {type} does not exist on {parentType}`");
                }
            ));

        /// <summary> Finds a type signature by a <paramref name="name" />. Will only look for type definitions (e.g. <see cref="String" />), not type references (<see cref="T:String[]" /> or <see cref="List{String}" />). </summary>
        /// <remarks>
        /// In case you'd like to get the specialized <see cref="TypeReference" /> you can use <seealso cref="TryFindType(string)" />
		/// </remarks>
        public TypeSignature TryFindTypeDef(string name)
        {
            var fullTypeName = new FullTypeName(name);
            if (this.definedTypeNames.TryGetValue(fullTypeName, out var result))
                return result.Signature;
            else
            {
                var t = Compilation.FindType(fullTypeName).GetDefinition();
                return t is object ? SymbolLoader.Type(t) : null;
            }
        }

        /// <summary> Finds a type signature by a <paramref name="name" />. Will only look for type definitions (e.g. <see cref="String" />), not type references (<see cref="T:String[]" /> or <see cref="List{String}" />). </summary>
        /// <remarks>
        /// In case you'd like to get the specialized <see cref="TypeReference" /> you can use <seealso cref="FindType(string)" />
		/// </remarks>
        public TypeSignature FindTypeDef(string name) =>
            TryFindTypeDef(name) ?? throw new ArgumentException($"Type {name} could not be found.", nameof(name));

        [Obsolete]
        public TypeSignature FindTypeDef(Type type) =>
            SymbolLoader.Type(Compilation.FindType(type).GetDefinition());


        /// <summary> Finds a type reference by a <paramref name="name" />. Will look for both type definitions (e.g. <see cref="String" />) and for type references (<see cref="T:String[]" /> or <see cref="List{String}" />). Returns null if the type can not be found. </summary>
        /// <remarks>
		/// Expected syntax: <c>NamespaceName '.' TopLevelTypeName ['`'#] { '+' NestedTypeName ['`'#] }</c>
		/// where # are type parameter counts.!--
        ///
        /// In case you'd like to get the generic <see cref="TypeSignature" /> you can use <seealso cref="TryFindTypeDef(string)" />
		/// </remarks>

        public TypeReference TryFindType(string name)
        {
            var fullTypeName = new FullTypeName(name);
            if (this.definedTypeNames.TryGetValue(fullTypeName, out var result))
                return result.Signature;
            else
            {
                var t = Compilation.FindType(fullTypeName);
                if (t is TS.Implementation.UnknownType)
                    return null;
                else
                    return SymbolLoader.TypeRef(t);
            }
        }

        /// <summary> Finds a type reference by a <paramref name="name" />. Will look for both type definitions (e.g. <see cref="String" />) and for type references (<see cref="T:String[]" /> or <see cref="List{String}" />). Throws an exception if the type can not be found. </summary>
        /// <remarks>
		/// Expected syntax: <c>NamespaceName '.' TopLevelTypeName ['`'#] { '+' NestedTypeName ['`'#] }</c>
		/// where # are type parameter counts.!--
        ///
        /// In case you'd like to get the generic <see cref="TypeSignature" /> you can use <seealso cref="FindTypeDef(string)" />
		/// </remarks>
        public TypeReference FindType(string name) =>
            TryFindType(name) ?? throw new ArgumentException($"Type reference {name} could be found.", nameof(name));

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

        /// <summary> Lists all method signatures of the specified <paramref name="type" />. Only includes the methods declared in this type, not the inherited ones. </summary>
        public IEnumerable<MethodSignature> GetMemberMethodDefs(TypeSignature type) =>
            this.definedTypes.GetValueOrDefault(type)?.Members.OfType<MethodDef>().Select(m => m.Signature) ??
            GetTypeDef(type).GetMethods(null, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Method);

        /// <summary> Lists all method signatures of the specified <paramref name="type" /> with specified <paramref name="name" />. Only includes the methods declared in this type, not the inherited ones. </summary>
        public IEnumerable<MethodSignature> GetMemberMethodDefs(TypeSignature type, string name) =>
            this.definedTypes.GetValueOrDefault(type)?.Members.OfType<MethodDef>().Where(m => m.Signature.Name == name).Select(m => m.Signature) ??
            GetTypeDef(type).GetMethods(m => m.Name == name, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Method);

        /// <summary> Lists all field signatures of the specified <paramref name="type" />. Only includes the fields declared in this type, not the inherited ones. </summary>
        public IEnumerable<FieldSignature> GetMemberFieldDefs(TypeSignature type) =>
            this.definedTypes.GetValueOrDefault(type)?.Members.OfType<FieldDef>().Select(m => m.Signature) ??
            GetTypeDef(type).GetFields(null, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Field);

        /// <summary> Gets the member field of the specified <paramref name="type" /> with specified <paramref name="name" />. Only includes the fields declared in this type, not the inherited ones. </summary>
        public FieldSignature GetMemberFieldDef(TypeSignature type, string name) =>
            this.definedTypes.TryGetValue(type, out var t) ? t.Members.OfType<FieldDef>().SingleOrDefault(m => m.Signature.Name == name)?.Signature :
            GetTypeDef(type).GetFields(f => f.Name == name, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Field).SingleOrDefault();

        /// <summary> Lists all property signatures of the specified <paramref name="type" />. Only includes the properties declared in this type, not the inherited ones. </summary>
        public IEnumerable<PropertySignature> GetMemberPropertyDefs(TypeSignature type) =>
            this.definedTypes.GetValueOrDefault(type)?.Members.OfType<PropertyDef>().Select(m => m.Signature) ??
            GetTypeDef(type).GetProperties(null, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Property);

        /// <summary> Gets the member property signature of the specified <paramref name="type" /> with specified <paramref name="name" />. Only includes the properties declared in this type, not the inherited ones. </summary>
        public PropertySignature GetMemberPropertyDef(TypeSignature type, string name) =>
            this.definedTypes.TryGetValue(type, out var t) ? t.Members.OfType<PropertyDef>().SingleOrDefault(m => m.Signature.Name == name)?.Signature :
            GetTypeDef(type).GetProperties(p => p.Name == name, GetMemberOptions.IgnoreInheritedMembers).Select(SymbolLoader.Property).SingleOrDefault();

        /// <summary> Lists all members of the specified <paramref name="type" />. Only includes the members declared in this type, not the inherited ones. </summary>
        public IEnumerable<MemberSignature> GetMemberDefs(TypeSignature type) =>
            GetMemberMethodDefs(type).AsEnumerable<MemberSignature>()
            .Concat(GetMemberPropertyDefs(type))
            .Concat(GetMemberFieldDefs(type));

        /// <summary> Lists all method references of the specified <paramref name="type" />. Only includes the methods declared in this type, not the inherited ones. </summary>
        public IEnumerable<MethodReference> GetMemberMethods(SpecializedType type, params TypeReference[] typeArgs) =>
            GetMemberMethodDefs(type.Type)
            .Where(m => m.TypeParameters.Length == typeArgs.Length)
            .Select(m => new MethodReference(m, type.TypeArguments, typeArgs.ToImmutableArray()));

        /// <summary> Lists all method references of the specified <paramref name="type" /> of the specified <paramref name="name" />. Only includes the methods declared in this type, not the inherited ones. </summary>
        public IEnumerable<MethodReference> GetMemberMethods(SpecializedType type, string name, params TypeReference[] typeArgs) =>
            GetMemberMethodDefs(type.Type, name)
            .Where(m => m.TypeParameters.Length == typeArgs.Length)
            .Select(m => new MethodReference(m, type.TypeArguments, typeArgs.ToImmutableArray()));

        /// <summary> Lists all field references of the specified <paramref name="type" />. Only includes the fields declared in this type, not the inherited ones. </summary>
        public IEnumerable<FieldReference> GetMemberFields(SpecializedType type) =>
            GetMemberFieldDefs(type.Type)
            .Select(f => new FieldReference(f, type.TypeArguments));

        /// <summary> Get the member field of the specified <paramref name="type" />with specified <paramref name="name" />. Only includes the fields declared in this type, not the inherited ones. </summary>
        public FieldReference GetMemberField(SpecializedType type, string name) =>
            GetMemberFieldDef(type.Type, name)
            ?.Apply(f => new FieldReference(f, type.TypeArguments));

        /// <summary> Lists all property references of the specified <paramref name="type" />. Only includes the properties declared in this type, not the inherited ones. </summary>
        public IEnumerable<PropertyReference> GetMemberProperties(SpecializedType type) =>
            GetMemberPropertyDefs(type.Type)
            .Select(p => new PropertyReference(p, type.TypeArguments));

        /// <summary> Get the member property of the specified <paramref name="type" /> with specified <paramref name="name" />. Only includes the properties declared in this type, not the inherited ones. </summary>
        public PropertyReference GetMemberProperty(SpecializedType type, string name) =>
            GetMemberPropertyDef(type.Type, name)
            ?.Apply(p => new PropertyReference(p, type.TypeArguments));

        /// <summary> Lists all member references of the specified <paramref name="type" />. Only includes the members declared in this type, not the inherited ones. Note that only methods without generic arguments are returned, if you want to get to them, use <see cref="GetMemberMethods(SpecializedType, TypeReference[])" /> </summary>
        public IEnumerable<MemberReference> GetMembers(SpecializedType type) =>
            GetMemberMethods(type).AsEnumerable<MemberReference>()
            .Concat(GetMemberProperties(type))
            .Concat(GetMemberFields(type));

        // /// <summary> Lists all method references of the specified <paramref name="type" />. Only includes the methods declared in this type, not the inherited ones. </summary>
        // public IEnumerable<MethodReference> GetMemberMethods(TypeReference type, params TypeReference[] typeArgs) =>
        //     type is TypeReference.SpecializedTypeCase st ? GetMemberMethods(st) :
        //     Enumerable.Empty<MethodReference>();

        // /// <summary> Lists all field references of the specified <paramref name="type" />. Only includes the fields declared in this type, not the inherited ones. </summary>
        // public IEnumerable<FieldReference> GetMemberFields(TypeReference type) =>
        //     GetMemberFieldDefs(type.Type)
        //     .Select(f => new FieldReference(f, type.GenericParameters));

        /// <summary> Lists all property references of the specified <paramref name="type" />. Only includes the properties declared in this type, not the inherited ones. </summary>
        public IEnumerable<PropertyReference> GetMemberProperties(TypeReference type) =>
            type.Match(
                specializedType => GetMemberProperties(specializedType),
                array => throw new NotImplementedException(),
                byRef => throw new NotImplementedException(),
                pointer => throw new NotImplementedException(),
                generic => throw new NotImplementedException(),
                function => Enumerable.Empty<PropertyReference>()
            );

        // /// <summary> Lists all member references of the specified <paramref name="type" />. Only includes the members declared in this type, not the inherited ones. Note that only methods without generic arguments are returned, if you want to get to them, use <see cref="GetMemberMethods(SpecializedType, TypeReference[])" /> </summary>
        // public IEnumerable<MemberReference> GetMembers(TypeReference type) =>
        //     GetMemberMethods(type).AsEnumerable<MemberReference>()
        //     .Concat(GetMemberProperties(type))
        //     .Concat(GetMemberFields(type));



        /// <summary> Gets all implemented interfaces of this specific <paramref name="type" />. Does not include the transitively implemented ones from its base types. </summary>
        public IEnumerable<SpecializedType> GetDirectImplements(SpecializedType type) =>
           (this.definedTypes.TryGetValue(type.Type, out var t) ? t.Implements :
            GetTypeDef(type.Type).DirectBaseTypes.Where(b => b.Kind == TypeKind.Interface).Select(SymbolLoader.TypeRef).Select(t => Assert.IsType<TypeReference.SpecializedTypeCase>(t).Item)
           )
            .Select(t => t.SubstituteGenerics(type.Type.AllTypeParameters(), type.TypeArguments));

        /// <summary> Gets all base type signatures of the specified <paramref name="type" />. </summary>
        public IEnumerable<SpecializedType> GetBaseTypes(SpecializedType type) =>
           (this.definedTypes.TryGetValue(type.Type, out var t) ? GetBaseTypes(t.Extends ?? TypeSignature.Object.NotGeneric()).Append(t.Extends ?? TypeSignature.Object.NotGeneric()) :
            GetTypeDef(type.Type).GetAllBaseTypes().Select(SymbolLoader.TypeRef).Select(t => Assert.IsType<TypeReference.SpecializedTypeCase>(t).Item)
           )
            .Select(t => t.SubstituteGenerics(type.Type.AllTypeParameters(), type.TypeArguments));


        private Dictionary<TypeSignature, List<ILSpyArbitraryTypeModification>> typeMods = new Dictionary<TypeSignature, List<ILSpyArbitraryTypeModification>>();

        /// <summary> The settings that controls how the code is generated set in <see cref="Create(IEnumerable{string}, EmitSettings)" />. Note that you can only read this property, if you'd like to change it, use the last parameter of the Create method. </summary>
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

        private List<(TypeDef type, Func<Exception, bool> errorHandler, bool isExternal)> waitingTypes = new List<(TypeDef, Func<Exception, bool>, bool)>();

        public IEnumerable<TypeDef> WaitingTypes => waitingTypes.Select(t => t.type);

        /// <summary> Adds a top level type definition that will be included in the generated code. </summary>
        /// <param name="errorHandler">The error handler will be invoked when an exception occurs during creating of this type. If not null, it will always catch the exception, so you may want to rethrow it with some more information about the generated type. When the handler returns `true` the error is treated as solved, when `false` the exception is rethrown.</param>
        /// <param name="isExternal">If true, the type will not be included in the generated config. It will be however treated as already present in the code, so the generated code will be aware that it exists.</param>
        public void AddType(TypeDef type, Func<Exception, bool> errorHandler = null, bool isExternal = false)
        {
            type = TypeFixer.Fix(type, this);
            this.definedTypes.Add(type.Signature, type);
            this.definedTypeNames.Add(type.Signature.GetFullTypeName(), type);
            this.waitingTypes.Add((type, errorHandler, isExternal));
        }

        /// <summary> Generates the "real" types from the symbolic <see cref="TypeDef" />s. When this method is called, all symbols used in the metadata must be already added to the context (using <see cref="AddType(TypeDef,Func{Exception, bool},bool)" />) </summary>
        public void CommitWaitingTypes()
        {
            var sortedTypes = MetadataDefiner.SortDefinitions(this.waitingTypes.Select(t => t.type).ToArray());
            var types =
                sortedTypes.Select(t => {
                    var vtype = MetadataDefiner.CreateTypeDefinition(this, t);
                    mutableModule.AddType(vtype);
                    return vtype;
                }).ToArray();
            var meta = this.waitingTypes.ToDictionary(t => t.type.Signature, t => t);
            foreach (var (type, vtype) in sortedTypes.ZipTuples(types))
            {
                var (_, errorHandler, isExternal) = meta[type.Signature];
                try
                {
                    MetadataDefiner.DefineTypeMembers(vtype, this, type, isExternal);
                }
                catch (Exception ex) when (errorHandler != null)
                {
                    var isSolved = errorHandler(ex);
                    if (!isSolved) throw;
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

        class FakeDocsProvider : IDocumentationProvider
        {
            public string GetDocumentation(IEntity entity)
            {
                return (entity as IWithDoccomment)?.Doccomment;
            }
        }

        private CSharpEmitter BuildCore()
        {
            CommitWaitingTypes();
            var s = new DecompilerSettings(LanguageVersion.Latest);
            s.CSharpFormattingOptions.AutoPropertyFormatting = PropertyFormatting.ForceOneLine;
            s.CSharpFormattingOptions.PropertyBraceStyle = BraceStyle.DoNotChange;

            var emitter = new CSharpEmitter(this.hackedCompilation, s, this.Settings.EmitPartialClasses);
            emitter.DocumentationProvider = new FakeDocsProvider();
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
                typeof(Uri).Assembly.GetName(),
                typeof(System.Text.RegularExpressions.Regex).Assembly.GetName(),
                typeof(System.Linq.Expressions.ExpressionType).Assembly.GetName(),
                typeof(System.ComponentModel.PropertyChangedEventHandler).Assembly.GetName(),
                typeof(System.IO.Directory).Assembly.GetName(),
                typeof(System.Xml.Linq.XElement).Assembly.GetName(),
                typeof(System.ComponentModel.ITypeDescriptorContext).Assembly.GetName(),
                typeof(System.ComponentModel.IContainer).Assembly.GetName(),
                typeof(System.IServiceProvider).Assembly.GetName(),
                typeof(System.Resources.IResourceWriter).Assembly.GetName(),
                typeof(System.Linq.IGrouping<string, int>).Assembly.GetName(),
                typeof(System.Collections.Generic.ISet<int>).Assembly.GetName(),
                typeof(System.Xml.XmlWriter).Assembly.GetName(),
                typeof(System.Drawing.Color).Assembly.GetName(),
                typeof(System.Collections.Specialized.BitVector32).Assembly.GetName(),
                typeof(System.Collections.Concurrent.ConcurrentDictionary<int, int>).Assembly.GetName(),
                typeof(System.Diagnostics.TraceSwitch).Assembly.GetName(),
                typeof(System.Security.Cryptography.SHA1).Assembly.GetName(),
                typeof(System.Diagnostics.FileVersionInfo).Assembly.GetName(),
                typeof(System.IO.StringReader).Assembly.GetName(),
                typeof(System.Console).Assembly.GetName()
                // new AssemblyName("netstandard")
            })
            let location = AssemblyLoadContext.Default.LoadFromAssemblyName(r).Location
            where !string.IsNullOrEmpty(location)
            let lUrl = new Uri(location)
            select Uri.UnescapeDataString(lUrl.AbsolutePath);

        private static Lazy<PEFile[]> ReferencedModules = new Lazy<PEFile[]>(() => GetReferencedPaths().Distinct().Select(a => new PEFile(a, System.Reflection.PortableExecutable.PEStreamOptions.PrefetchMetadata)).ToArray());
    }
}
