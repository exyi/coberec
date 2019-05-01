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

namespace Coberec.ExprCS
{
    public class MetadataContext
    {
        private readonly HackedSimpleCompilation hackedCompilation;
        internal ICompilation Compilation => hackedCompilation;


        internal MetadataContext(HackedSimpleCompilation compilation)
        {
            this.hackedCompilation = compilation;

            moduleMap = compilation.Modules.ToDictionary(m => new ModuleSignature(m.Name));
            Modules = moduleMap.Keys.ToImmutableArray();
            MainModule = Modules[0];
            mutableModule = (VirtualModule)moduleMap[MainModule];

            Debug.Assert(MainModule.Name == this.Compilation.MainModule.Name);
        }

        public static MetadataContext Create(
            string mainModuleName,
            string[] references = null)
        {
            var referencedModules =
                references == null ? ReferencedModules.Value :
                references.Distinct().Select(r => new PEFile(r, PEStreamOptions.PrefetchMetadata)).ToArray();

            var compilation = new HackedSimpleCompilation(
                new VirtualModuleReference(true, mainModuleName),
                referencedModules
            );

            return new MetadataContext(compilation);
        }
        public ImmutableArray<ModuleSignature> Modules { get; }
        public ModuleSignature MainModule { get; }
        internal IModule MainILSpyModule => mutableModule;
        readonly VirtualModule mutableModule;
        private readonly Dictionary<ModuleSignature, IModule> moduleMap;
        readonly ConcurrentDictionary<ITypeDefinition, TypeSignature> typeSignatureCache = new ConcurrentDictionary<ITypeDefinition, TypeSignature>();
        TypeSignature TranslateType(ITypeDefinition t) =>
            typeSignatureCache.GetOrAdd(t, type => {
                var parent = type.DeclaringTypeDefinition != null ?
                             TypeOrNamespace.TypeSignature(TranslateType(type.DeclaringTypeDefinition)) :
                             TypeOrNamespace.NamespaceSignature(TranslateNamespace(type.Namespace));
                return new TypeSignature(type.Name, parent, type.IsSealed, type.IsAbstract, TranslateAccessibility(type.Accessibility), type.TypeParameterCount);
            });

        readonly ConcurrentDictionary<IMethod, MethodSignature> methodSignatureCache = new ConcurrentDictionary<IMethod, MethodSignature>();
        MethodSignature TranslateMethod(IMethod method) =>
            methodSignatureCache.GetOrAdd(method, m =>
                new MethodSignature(
                    TranslateType(m.DeclaringType.GetDefinition()),
                    m.Parameters.Select(TranslateArgument).ToImmutableArray(),
                    m.Name,
                    TranslateTypeReference(m.ReturnType),
                    m.IsStatic,
                    TranslateAccessibility(m.Accessibility),
                    m.IsVirtual,
                    m.IsOverride,
                    m.IsAbstract,
                    m.Name.Contains("."), // TODO: fix this heuristic
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
            namespaceSignatureCache.GetOrAdd(ns, n => {
                var dot = n.LastIndexOf('.');
                if (dot < 0) return new NamespaceSignature(n, null);
                return new NamespaceSignature(
                    n.Substring(dot + 1),
                    TranslateNamespace(n.Substring(0, dot))
                );
            });

        MemberSignature TranslateMember(IMember m) =>
            m is ITypeDefinition type ? TranslateType(type) :
            m is IMethod method       ? (MemberSignature)TranslateMethod(method) :
            throw new NotSupportedException($"Member '{m}' of type '{m.GetType().Name}' is not supported");

        MethodArgument TranslateArgument(IParameter parameter) =>
            new MethodArgument(
                TranslateTypeReference(parameter.Type),
                parameter.Name
            );

        TypeReference TranslateTypeReference(IType type) =>
            type is ITypeDefinition td ? TypeReference.SpecializedType(TranslateType(td), ImmutableArray<TypeReference>.Empty) :
            type is TS.ByReferenceType refType ? TypeReference.ByReferenceType(TranslateTypeReference(refType.ElementType)) :
            type is TS.PointerType ptrType ? TypeReference.PointerType(TranslateTypeReference(ptrType.ElementType)) :
            type is TS.ArrayType arrType ? TypeReference.ArrayType(TranslateTypeReference(arrType.ElementType), arrType.Dimensions) :
            type is TS.ParameterizedType paramType ? TypeReference.SpecializedType(
                                                         TranslateType(paramType.GenericType.GetDefinition()),
                                                         paramType.TypeArguments.Select(TranslateTypeReference).ToImmutableArray()) :
            type is TS.ITypeParameter typeParam ? TypeReference.GenericParameter(TranslateGenericParameter(typeParam)) :
            throw new NotImplementedException($"Type reference '{type}' of type '{type.GetType().Name}' is not supported.");

        internal IModule GetModule(ModuleSignature module) =>
            moduleMap.TryGetValue(module, out var result) ? result :
            throw new ArgumentException($"Module {module} is not known.");

        internal INamespace GetNamespace(NamespaceSignature ns) =>
            ns == null ? Compilation.RootNamespace :
            GetNamespace(ns.Parent).GetChildNamespace(ns.Name);

        internal ITypeDefinition GetTypeDef(TypeSignature type) =>
            type.Parent.Match(
                ns => GetNamespace(ns.Item).GetTypeDefinition(type.Name, type.GenericArgCount) ?? throw new InvalidOperationException($"Type {type} could not be found"),
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
            GetTypeDef(type).GetMethods().Select(TranslateMethod);

        public IEnumerable<FieldSignature> GetMemberFields(TypeSignature type) =>
            GetTypeDef(type).GetFields().Select(TranslateField);

        public IEnumerable<MemberSignature> GetMembers(TypeSignature type) =>
            GetMemberMethods(type).AsEnumerable<MemberSignature>()
            .Concat(GetMemberFields(type));


        public void AddType(TypeDef type)
        {
            var xx = MetadataDefiner.CreateTypeDefinition(this, type);
            mutableModule.AddType(xx);
            MetadataDefiner.DefineTypeMembers(xx, this, type);
        }

        private CSharpEmitter BuildCore()
        {
            var s = new DecompilerSettings(LanguageVersion.Latest);
            s.CSharpFormattingOptions.AutoPropertyFormatting = PropertyFormatting.ForceOneLine;
            s.CSharpFormattingOptions.PropertyBraceStyle = BraceStyle.DoNotChange;

            var emitter = new CSharpEmitter(this.hackedCompilation, s, false);
            return emitter;
        }

        public string EmitToString()
        {
            var e = BuildCore();
            return e.DecompileWholeModuleAsString();
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
