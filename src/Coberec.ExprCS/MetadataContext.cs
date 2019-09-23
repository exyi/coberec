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
        readonly ConcurrentDictionary<ITypeDefinition, TypeSignature> typeSignatureCache = new ConcurrentDictionary<ITypeDefinition, TypeSignature>();
        internal TypeSignature TranslateType(ITypeDefinition t) =>
            typeSignatureCache.GetOrAdd(t, type => {
                var parent = type.DeclaringTypeDefinition != null ?
                             TypeOrNamespace.TypeSignature(TranslateType(type.DeclaringTypeDefinition)) :
                             TypeOrNamespace.NamespaceSignature(TranslateNamespace(type.Namespace));
                var kind = t.Kind == TypeKind.Interface ? "interface" :
                           t.Kind == TypeKind.Struct ? "struct" :
                           t.Kind == TypeKind.Class ? "class" :
                           t.Kind == TypeKind.Void ? "struct" :
                           t.Kind == TypeKind.Enum ? "enum" :
                           t.Kind == TypeKind.Delegate ? "delegate" :
                           throw new NotSupportedException($"Type kind {t.Kind} is not supported.");
                return new TypeSignature(type.Name, parent, kind, isValueType: !(bool)type.IsReferenceType, canOverride: !type.IsSealed && !type.IsStatic, isAbstract: type.IsAbstract || type.IsStatic, TranslateAccessibility(type.Accessibility), type.TypeParameterCount);
            });

        readonly ConcurrentDictionary<IMethod, MethodSignature> methodSignatureCache = new ConcurrentDictionary<IMethod, MethodSignature>();
        public MethodSignature TranslateMethod(IMethod method) =>
            methodSignatureCache.GetOrAdd(method, m =>
                new MethodSignature(
                    TranslateType(m.DeclaringType.GetDefinition()),
                    m.Parameters.Select(TranslateParameter).ToImmutableArray(),
                    m.Name,
                    TranslateTypeReference(m.ReturnType),
                    m.IsStatic,
                    TranslateAccessibility(m.Accessibility),
                    m.IsVirtual,
                    m.IsOverride,
                    m.IsAbstract,
                    m.IsConstructor || m.IsAccessor || m.IsOperator || m.IsDestructor,
                    m.TypeParameters.Select(TranslateGenericParameter).ToImmutableArray()
                )
            );

        readonly ConcurrentDictionary<IField, FieldSignature> fieldSignatureCache = new ConcurrentDictionary<IField, FieldSignature>();
        FieldSignature TranslateField(IField field) =>
            fieldSignatureCache.GetOrAdd(field, f =>
                new FieldSignature(
                    TranslateType(f.DeclaringType.GetDefinition()),
                    f.Name,
                    TranslateAccessibility(field.Accessibility),
                    TranslateTypeReference(f.ReturnType),
                    f.IsStatic,
                    f.IsReadOnly
                )
            );

        readonly ConcurrentDictionary<IProperty, PropertySignature> propSignatureCache = new ConcurrentDictionary<IProperty, PropertySignature>();
        PropertySignature TranslateProperty(IProperty property) =>
            propSignatureCache.GetOrAdd(property, p =>
                new PropertySignature(
                    TranslateType(p.DeclaringType.GetDefinition()),
                    TranslateTypeReference(p.ReturnType),
                    p.Name,
                    TranslateAccessibility(p.Accessibility),
                    p.IsStatic,
                    p.Getter?.Apply(TranslateMethod),
                    p.Setter?.Apply(TranslateMethod)
                )
            );

        ConcurrentDictionary<ITypeParameter, GenericParameter> typeParameterCache = new ConcurrentDictionary<ITypeParameter, GenericParameter>();
        GenericParameter TranslateGenericParameter(ITypeParameter parameter) =>
            typeParameterCache.GetOrAdd(parameter, p => new GenericParameter(Guid.NewGuid(), p.Name));

        Accessibility TranslateAccessibility(TS.Accessibility a) =>
            a == TS.Accessibility.Internal ? Accessibility.AInternal :
            a == TS.Accessibility.Private ? Accessibility.APrivate :
            a == TS.Accessibility.Public ? Accessibility.APublic :
            a == TS.Accessibility.Protected ? Accessibility.AProtected :
            a == TS.Accessibility.ProtectedOrInternal ? Accessibility.AProtectedInternal :
            a == TS.Accessibility.ProtectedAndInternal ? Accessibility.APrivateProtected :
            throw new NotSupportedException($"{a}");

        ConcurrentDictionary<string, NamespaceSignature> namespaceSignatureCache = new ConcurrentDictionary<string, NamespaceSignature>();
        NamespaceSignature TranslateNamespace(string ns) =>
            namespaceSignatureCache.GetOrAdd(ns, NamespaceSignature.Parse);

        MemberSignature TranslateMember(IMember m) =>
            m is ITypeDefinition type ? TranslateType(type) :
            m is IMethod method       ? (MemberSignature)TranslateMethod(method) :
            throw new NotSupportedException($"Member '{m}' of type '{m.GetType().Name}' is not supported");

        internal MethodParameter TranslateParameter(IParameter parameter) =>
            new MethodParameter(
                TranslateTypeReference(parameter.Type),
                parameter.Name
            );

        internal TypeReference TranslateTypeReference(IType type) =>
            type is ITypeDefinition td ? TypeReference.SpecializedType(TranslateType(td), ImmutableArray<TypeReference>.Empty) :
            type is TS.ByReferenceType refType ? TypeReference.ByReferenceType(TranslateTypeReference(refType.ElementType)) :
            type is TS.PointerType ptrType ? TypeReference.PointerType(TranslateTypeReference(ptrType.ElementType)) :
            type is TS.ArrayType arrType ? TypeReference.ArrayType(TranslateTypeReference(arrType.ElementType), arrType.Dimensions) :
            type is TS.ParameterizedType paramType ? TypeReference.SpecializedType(
                                                         TranslateType(paramType.GenericType.GetDefinition()),
                                                         paramType.TypeArguments.Select(TranslateTypeReference).ToImmutableArray()) :
            type is TS.ITypeParameter typeParam ? TypeReference.GenericParameter(TranslateGenericParameter(typeParam)) :
            type is TS.Implementation.NullabilityAnnotatedType decoratedType ? TranslateTypeReference(decoratedType.TypeWithoutAnnotation) :
            throw new NotImplementedException($"Type reference '{type}' of type '{type.GetType().Name}' is not supported.");

        internal IModule GetModule(ModuleSignature module) =>
            moduleMap.TryGetValue(module, out var result) ? result :
            throw new ArgumentException($"Module {module} is not known.");

        internal INamespace GetNamespace(NamespaceSignature ns) =>
            ns == NamespaceSignature.Global ? Compilation.RootNamespace :
            GetNamespace(ns.Parent).GetChildNamespace(ns.Name) ?? throw new Exception($"Could not resolve namespace {ns}.");

        internal ITypeDefinition GetTypeDef(TypeSignature type) =>
            type.Parent.Match(
                ns => GetNamespace(ns.Item).GetTypeDefinition(type.Name, type.GenericParamCount) ?? throw new InvalidOperationException($"Type {type} could not be found"),
                parentType => GetTypeDef(parentType.Item).GetNestedTypes(t => t.Name == type.Name).Single().GetDefinition());

        public TypeSignature FindTypeDef(string name) =>
            TranslateType(Compilation.FindType(new FullTypeName(name)).GetDefinition());
        public TypeSignature FindTypeDef(Type type) =>
            TranslateType(Compilation.FindType(type).GetDefinition());


        public TypeReference FindType(string name) =>
            TranslateTypeReference(Compilation.FindType(new FullTypeName(name)));
        public TypeReference FindType(Type type) =>
            TranslateTypeReference(Compilation.FindType(type));

        public IEnumerable<TypeSignature> GetTopLevelTypes() =>
            Compilation.GetTopLevelTypeDefinitions().Select(TranslateType);

        public IEnumerable<TypeSignature> GetTopLevelTypes(ModuleSignature module) =>
            GetModule(module).TopLevelTypeDefinitions.Select(TranslateType);

        public IEnumerable<TypeOrNamespace> GetNamespaceMembers(NamespaceSignature @namespace)
        {
            var ns = GetNamespace(@namespace);
            return ns.ChildNamespaces.Select(n => TranslateNamespace(n.FullName)).Select(TypeOrNamespace.NamespaceSignature)
                   .Concat(ns.Types.Select(TranslateType).Select(TypeOrNamespace.TypeSignature));
        }

        public IEnumerable<MethodSignature> GetMemberMethods(TypeSignature type) =>
            GetTypeDef(type).GetMethods(null, GetMemberOptions.IgnoreInheritedMembers).Select(TranslateMethod);

        public IEnumerable<FieldSignature> GetMemberFields(TypeSignature type) =>
            GetTypeDef(type).GetFields(null, GetMemberOptions.IgnoreInheritedMembers).Select(TranslateField);

        public IEnumerable<PropertySignature> GetMemberProperties(TypeSignature type) =>
            GetTypeDef(type).GetProperties(null, GetMemberOptions.IgnoreInheritedMembers).Select(TranslateProperty);

        public IEnumerable<SpecializedType> GetBaseTypes(TypeSignature type) =>
            GetTypeDef(type).GetAllBaseTypes().Select(TranslateTypeReference).Select(t => Assert.IsType<TypeReference.SpecializedTypeCase>(t).Item);

        public IEnumerable<SpecializedType> GetDirectImplements(TypeSignature type) =>
            GetTypeDef(type).DirectBaseTypes.Where(b => b.Kind == TypeKind.Interface).Select(TranslateTypeReference).Select(t => Assert.IsType<TypeReference.SpecializedTypeCase>(t).Item);

        public IEnumerable<MemberSignature> GetMembers(TypeSignature type) =>
            GetMemberMethods(type).AsEnumerable<MemberSignature>()
            .Concat(GetMemberProperties(type))
            .Concat(GetMemberFields(type));

        private List<TypeDef> definedTypes = new List<TypeDef>();
        public IReadOnlyList<TypeDef> DefinedTypes => definedTypes.AsReadOnly();

        public EmitSettings Settings { get; }

        public void AddType(TypeDef type)
        {
            var xx = MetadataDefiner.CreateTypeDefinition(this, type);
            mutableModule.AddType(xx);
            MetadataDefiner.DefineTypeMembers(xx, this, type);
            definedTypes.Add(type);
        }

// #if ExposeILSpy
        public TypeSignature AddRawType(ITypeDefinition type)
        {
            mutableModule.AddType(type);
            return TranslateType(type);
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
