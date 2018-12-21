using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using ICSharpCode.Decompiler.IL;
using IL = ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using TrainedMonkey.CSharpGen.TypeSystem;
using System.Diagnostics;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler;
using TrainedMonkey.CSharpGen.Emit;
using System.Linq.Expressions;
using System.Reflection;
using System.Globalization;
using System.Text;
using TrainedMonkey.MetaSchema;
using System.Collections.Immutable;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;

namespace TrainedMonkey.CSharpGen
{
    public sealed class PrimitiveTypeMapping
    {
        public PrimitiveTypeMapping(string @namespace, string name)
        {
            Name = name;
            Namespace = @namespace;
        }

        public string Name { get; }
        public string Namespace { get; }
    }
    public sealed class EmitSettings
    {
        public EmitSettings(string @namespace, ImmutableDictionary<string, FullTypeName> primitiveTypeMapping)
        {
            Namespace = @namespace;
            PrimitiveTypeMapping = primitiveTypeMapping;
        }

        public bool EmitInterfaceWithMethods { get; } = true;
        public bool EmitOptionalWithMethods { get; } = true;
        public bool EmitWithMethods { get; } = true;

        public string Namespace { get; }
        public ImmutableDictionary<string, FullTypeName> PrimitiveTypeMapping { get; }
    }

    public sealed class TypeSymbolNameMapping
    {
        public TypeSymbolNameMapping(string name, Dictionary<string, string> fields = null, Dictionary<string, string> specialSymbols = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name), "Every type has a name");
            Fields = fields;
            SpecialSymbols = specialSymbols;
        }

        public string Name { get; }
        public Dictionary<string, string> Fields { get; }
        public Dictionary<string, string> SpecialSymbols { get; }

        public string GetSymbol(string name) =>
            SpecialSymbols?.GetValueOrDefault(name);
    }

    public sealed class CSharpBackend
    {
        private readonly EmitContext cx;
        private Dictionary<string, TypeDef> typeSchemas;
        private Dictionary<string, VirtualType> prebuiltTypes = new Dictionary<string, VirtualType>();
        private Dictionary<string, (VirtualType type, TypeSymbolNameMapping mapping)> realizedTypes = new Dictionary<string, (VirtualType, TypeSymbolNameMapping)>();
        private CSharpBackend(EmitContext cx)
        {
            this.cx = cx;
        }
        VirtualType AddType(EmitContext cx, TypeDef def)
        {
            var name = SymbolNamer.NameType(cx.Settings.Namespace, def.Name, cx);

            var isAbstract = def.Core is TypeDefCore.UnionCase;
            var typeKind = def.Core is TypeDefCore.InterfaceCase ? TypeKind.Interface :
                           TypeKind.Class;

            var type = new VirtualType(
                typeKind,
                Accessibility.Public,
                new FullTypeName(new TopLevelTypeName(cx.Settings.Namespace, name)),
                isStatic: false,
                isSealed: !isAbstract && typeKind == TypeKind.Class,
                isAbstract: isAbstract,
                parentModule: cx.Module
            );
            cx.Module.AddType(type);
            return type;
        }

        IType FindType(string name) =>
            this.prebuiltTypes.TryGetValue(name, out var propType) ? (IType)propType :
            cx.Settings.PrimitiveTypeMapping.TryGetValue(name, out var fullName) ? cx.FindType(fullName) :
            // throw new Exception($"Could not resolve type '{name}'");
            cx.FindType<string>();

        IType FindType(TypeRef type) =>
            type.Match(a => FindType(a.TypeName),
                       n =>
                       {
                           var t = FindType(n.Type);
                           if (t.IsReferenceType == false) return new ParameterizedType(cx.FindType(typeof(Nullable<>)), new[] { t });
                           else return t;
                       },
                       l =>
                       {
                           var t = FindType(l.Type);
                           return new ParameterizedType(cx.FindType(typeof(ImmutableArray<>)), new[] { t });
                       });

        (VirtualType, TypeSymbolNameMapping) BuildType(TypeDef def)
        {
            if (this.realizedTypes.TryGetValue(def.Name, out var result))
                return result;

            var type = this.prebuiltTypes[def.Name];

            var mapping = def.Core.Match(
                composite: composite => GenerateComposite(type, composite),
                primitive: primitive => GenerateScalar(type, primitive),
                union: union => GenerateUnion(type, union),
                @interface: ifc => GenerateInterface(type, ifc));

            result = (type, mapping);
            this.realizedTypes.Add(def.Name, result);
            return result;
        }

        private TypeSymbolNameMapping GenerateInterface(VirtualType type, TypeDefCore.InterfaceCase ifc)
        {
            var specialSymbols = new Dictionary<string, string>();
            var propDictionary = new Dictionary<string, (TypeField schema, IProperty prop)>();
            var props = new List<(TypeField schema, IProperty prop)>();

            foreach (var f in ifc.Fields)
            {
                var propType = FindType(f.Type);
                var prop = type.AddInterfaceProperty(f.Name, propType);
                propDictionary.Add(f.Name, (f, prop));
                props.Add((f, prop));
            }

            // type.ImplementEquality(properties);

            if (cx.Settings.EmitInterfaceWithMethods)
            {
                var withMethod = type.InterfaceWithMethod(props.Select(p => (p.prop as IMember, p.schema.Name)).ToArray(), isOptional: cx.Settings.EmitOptionalWithMethods && props.Count > 1);

                specialSymbols.Add("With", withMethod.Name);
            }

            return new TypeSymbolNameMapping(
                type.Name,
                props.ToDictionary(p => p.schema.Name, p => p.prop.Name),
                specialSymbols: specialSymbols
            );
        }

        private TypeSymbolNameMapping GenerateScalar(VirtualType type, TypeDefCore.PrimitiveCase primitive)
        {
            var valueProperty = type.AddAutoProperty("Value", cx.FindType<string>());

            type.AddCreateConstructor(cx, new[] { ("value", valueProperty.field) });
            type.ImplementEquality(new[] { valueProperty.prop });

            return new TypeSymbolNameMapping(type.Name);
        }

        private TypeSymbolNameMapping GenerateComposite(VirtualType type, TypeDefCore.CompositeCase composite)
        {
            var propDictionary = new Dictionary<string, (TypeField schema, IProperty prop, IField field)>();
            var props = new List<(TypeField schema, IProperty prop, IField field)>();

            foreach (var f in composite.Fields)
            {
                var propType = FindType(f.Type);
                var (prop, field) = type.AddAutoProperty(f.Name, propType);
                propDictionary.Add(f.Name, (f, prop, field));
                props.Add((f, prop, field));
            }

            var ctor = type.AddCreateConstructor(cx, props.Select(k => (k.schema.Name, k.field)).ToArray());
            var properties = props.Select(p => p.prop).ToArray();

            type.ImplementEquality(properties);

            IMethod withMethod = null;
            if (cx.Settings.EmitWithMethods)
            {
                withMethod = type.ImplementWithMethod(ctor, properties);
                if (cx.Settings.EmitOptionalWithMethods && properties.Length > 1)
                    type.ImplementOptionalWithMethod(withMethod, properties);
            }

            foreach (var f in composite.Implements)
            {
                var interfaceName = ((TypeRef.ActualTypeCase)f).TypeName;
                var interfaceDeclaration = this.typeSchemas[interfaceName];
                var (interfaceType, interfaceMapping) = this.BuildType(interfaceDeclaration);
                type.ImplementedInterfaces.Add(interfaceType);

                var interfaceFields =
                   (from ifcProp in ((TypeDefCore.InterfaceCase)interfaceDeclaration.Core).Fields
                    select (
                        ifcProp,
                        myProp: propDictionary[ifcProp.Name],
                        ifcRealProp: interfaceMapping.Fields[ifcProp.Name]
                                     .Apply(n => interfaceType.GetProperties(p => p.Name == n))
                                     .Single()
                    )

                   ).ToArray();
                foreach (var (ifcProp, myProp, ifcRealProp) in interfaceFields)
                {
                    if (myProp.prop == null ||
                        myProp.prop.Name != ifcRealProp.Name ||
                        !myProp.prop.ReturnType.Equals(ifcRealProp.ReturnType))
                        // add explicit implementation if needed
                        type.AddExplicitInterfaceProperty(ifcRealProp, myProp.prop);
                }

                if (interfaceMapping.GetSymbol("With") is string withMethodName)
                {
                    var ifcWithMethod = interfaceType.GetMethods(m => m.Name == withMethodName).Single();

                    if (withMethod == null) throw new NotSupportedException($"Could not implement {interfaceName} for {type.Name} due to conflict in With method settings.");

                    type.InterfaceImplementationWithMethod(
                        withMethod,
                        ifcWithMethod,
                        interfaceFields.Select(x =>
                            (x.myProp.prop as IMember, x.ifcProp.Name)).ToArray(),
                        properties
                    );
                }
            }

            return new TypeSymbolNameMapping(
                type.Name,
                props.ToDictionary(p => p.schema.Name, p => p.prop.Name)
            );
        }

        private TypeSymbolNameMapping GenerateUnion(VirtualType type, TypeDefCore.UnionCase union)
        {
            // var sealMethodName = SymbolNamer.NameMethod(type, "Seal", 0, new IType[0]);
            // type.Methods.Add(new VirtualMethod(type, Accessibility.ProtectedAndInternal, sealMethodName, new IParameter[0], cx.FindType(typeof(void)), isAbstract: true));
            var (abstractEqCore, _) = type.ImplementEqualityForBase();

            var cases = union.Options.Select((schema, index) =>
            {
                string name(TypeRef t) =>
                    t.Match(
                        actual: x => x.TypeName,
                        nullable: x => name(x.Type),
                        list: x => name(x.Type) + "List"
                    );
                var caseName = name(schema);

                var caseType = new VirtualType(TypeKind.Class, Accessibility.Public,
                    type.FullTypeName.NestedType(SymbolNamer.NameMember(type, caseName + "Case", lowerCase: false), 0),
                    isStatic: false,
                    isSealed: true,
                    isAbstract: false,
                    declaringType: type
                );
                caseType.DirectBaseType = type;
                type.NestedTypes.Add(caseType);
                return (index, schema, caseName, caseType);
            }).ToArray();

            var baseMatch = type.ImplementMatchBase(cases.Select(c => ((IType)c.caseType, c.caseName)).ToArray());

            foreach (var (index, schema, caseName, caseType) in cases)
            {
                var valueType = FindType(schema);

                // var sealMethod = new VirtualMethod(caseType, Accessibility.ProtectedAndInternal, sealMethodName, new IParameter[0], cx.FindType(typeof(void)), isOverride: true);
                // sealMethod.BodyFactory = () => EmitExtensions.CreateOneBlockFunction(sealMethod);
                // caseType.Methods.Add(sealMethod);

                var valueProperty = caseType.AddAutoProperty("Item", valueType);
                var caseCtor = caseType.AddCreateConstructor(cx, new[] { ("item", valueProperty.field) });

                caseType.ImplementEqualityForCase(abstractEqCore, valueProperty.prop);
                caseType.ImplementMatchCase(baseMatch, index);

                var caseFactory = new VirtualMethod(type, Accessibility.Public,
                    SymbolNamer.NameMethod(type, caseName, 0, new IType[] { valueType }),
                    new[] { new DefaultParameter(valueType, "item") },
                    returnType: type,
                    isStatic: true
                );
                caseFactory.BodyFactory = () =>
                    EmitExtensions.CreateExpressionFunction(caseFactory,
                        new IL.NewObj(caseCtor) { Arguments = { new IL.LdLoc(new IL.ILVariable(VariableKind.Parameter, valueType, 0)) } }
                    );
                type.Methods.Add(caseFactory);
            }

            return new TypeSymbolNameMapping(
                type.Name,
                cases.ToDictionary(c => c.caseName, c => c.caseType.Name)
            );
        }

        public static string Build(DataSchema schema, EmitSettings settings)
        {
            var cx = new EmitContext(
                new HackedSimpleCompilation(
                    new VirtualModuleReference(true, "NewEpicModule"),
                    ReferencedModules.Value
                ),
                settings,
                schema
            );

            var @this = new CSharpBackend(cx);

            @this.typeSchemas = schema.Types.ToDictionary(t => t.Name);

            @this.prebuiltTypes = schema.Types.ToDictionary(t => t.Name, t => @this.AddType(cx, t));

            foreach (var t in schema.Types)
                @this.BuildType(t);

            var s = new DecompilerSettings(LanguageVersion.Latest);
            s.CSharpFormattingOptions.AutoPropertyFormatting = PropertyFormatting.ForceOneLine;
            s.CSharpFormattingOptions.PropertyBraceStyle = BraceStyle.DoNotChange;

            var emitter = new CSharpEmitter(cx.HackedSimpleCompilation, s);
            var result = emitter.DecompileWholeModuleAsString();

            return result;
        }

        public static IEnumerable<string> GetReferencedPaths() =>
            from r in Enumerable.Concat(typeof(CSharpBackend).Assembly.GetReferencedAssemblies(), new[] {
                typeof(string).Assembly.GetName(),
                typeof(System.Collections.StructuralComparisons).Assembly.GetName(),
                typeof(ValueTuple<int, int>).Assembly.GetName()
                // new AssemblyName("netstandard")
            })
            let location = AssemblyLoadContext.Default.LoadFromAssemblyName(r).Location
            where !string.IsNullOrEmpty(location)
            let lUrl = new Uri(location)
            select lUrl.AbsolutePath;

        private static Lazy<PEFile[]> ReferencedModules = new Lazy<PEFile[]>(() => GetReferencedPaths().Select(a => new PEFile(a, System.Reflection.PortableExecutable.PEStreamOptions.PrefetchMetadata)).ToArray());
    }

    public sealed class EmitContext
    {
        public EmitContext(HackedSimpleCompilation hackedSimpleCompilation, EmitSettings settings, DataSchema fullSchema)
        {
            HackedSimpleCompilation = hackedSimpleCompilation;
            Module = (VirtualModule)Compilation.MainModule;
            Settings = settings;
            FullSchema = fullSchema;
        }

        public HackedSimpleCompilation HackedSimpleCompilation { get; }

        public VirtualModule Module { get; }

        public ICompilation Compilation => HackedSimpleCompilation;

        public EmitSettings Settings { get; }
        public DataSchema FullSchema { get; }

        public IType FindType(Type t) => Compilation.FindType(t);
        public IType FindType<T>() => Compilation.FindType(typeof(T));
        public IType FindType(FullTypeName name) => new GetClassTypeReference(name).Resolve(new SimpleTypeResolveContext(Compilation));
        public IMethod FindMethod<TResult>(Expression<Func<TResult>> expr)
        {
            var body = expr.Body;
            var methodInfo = body is MethodCallExpression call ? call.Method :
                                body is NewExpression @new ? (MethodBase)@new.Constructor :
                                throw new NotSupportedException($"Expression '{expr}' of type '{body}' is not supported");

            var t = FindType(methodInfo.DeclaringType);
            var parameters = methodInfo.GetParameters();
            // TODO: also check arg types
            var method = t.GetDefinition().Methods.Where(m => m.Name == methodInfo.Name && m.Parameters.Count == parameters.Length).Single();

            var methodGenericArgs = methodInfo.IsGenericMethod ?
                                    methodInfo.GetGenericArguments().Select(FindType).ToArray() :
                                    null;
            var typeGenericArgs = methodInfo.DeclaringType.IsGenericType ?
                                    methodInfo.DeclaringType.GetGenericArguments().Select(FindType).ToArray() :
                                    null;


            if (typeGenericArgs != null || methodGenericArgs != null)
                method = method.Specialize(new TypeParameterSubstitution(typeGenericArgs, methodGenericArgs));

            return method;
        }
    }
}
