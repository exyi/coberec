using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using ICSharpCode.Decompiler.IL;
using IL=ICSharpCode.Decompiler.IL;
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

        public string Namespace { get; }
        public ImmutableDictionary<string, FullTypeName> PrimitiveTypeMapping { get; }
    }

    public sealed class CSharpBackend
    {
        VirtualType AddType(EmitContext cx, TypeDef def)
        {
            var name = SymbolNamer.NameType(cx.Settings.Namespace, def.Name, cx);

            var isAbstract = def.Core is TypeDefCore.UnionCase;

            var type = new VirtualType(
                TypeKind.Class,
                Accessibility.Public,
                new FullTypeName(new TopLevelTypeName(cx.Settings.Namespace, name)),
                isStatic: false,
                isSealed: !isAbstract,
                isAbstract: isAbstract,
                parentModule: cx.Module
            );
            cx.Module.AddType(type);
            return type;
        }

        IType FindType(EmitContext cx, string name) =>
            cx.GeneratedTypes.TryGetValue((cx.Settings.Namespace, name), out var propType) ? (IType)propType :
            cx.Settings.PrimitiveTypeMapping.TryGetValue(name, out var fullName) ? cx.FindType(fullName) :
            // throw new Exception($"Could not resolve type '{name}'");
            cx.FindType<string>();

        IType FindType(EmitContext cx, TypeRef type) =>
            type.Match(a => FindType(cx, a.TypeName),
                       n => {
                           var t = FindType(cx, n.Type);
                           if (t.IsReferenceType == false) return new ParameterizedType(cx.FindType(typeof(Nullable<>)), new [] { t });
                           else return t;
                       },
                       l => {
                           var t = FindType(cx, l.Type);
                           return new ParameterizedType(cx.FindType(typeof(ImmutableArray<>)), new [] { t });
                       });


        void BuildType(EmitContext cx, TypeDef def)
        {
            var type = cx.GeneratedTypes[(cx.Settings.Namespace, def.Name)];

            switch(def.Core) {
                case TypeDefCore.CompositeCase composite:
                    GenerateComposite(cx, type, composite);
                    break;
                case TypeDefCore.PrimitiveCase primitive:
                    GenerateScalar(cx, type, primitive);
                    break;
                case TypeDefCore.UnionCase union:
                    GenerateUnion(cx, type, union);
                    break;
                default:
                    break;
            }
        }

        private void GenerateScalar(EmitContext cx, VirtualType type, TypeDefCore.PrimitiveCase primitive)
        {
            var valueProperty = type.AddAutoProperty("Value", cx.FindType<string>());

            type.AddCreateConstructor(cx, new [] { ("value", valueProperty.field) });
            type.ImplementEquality(new [] { valueProperty.prop });
        }

        private void GenerateComposite(EmitContext cx, VirtualType type, TypeDefCore.CompositeCase composite)
        {
            var props = new Dictionary<string, (IProperty prop, IField field)>();

            foreach (var f in composite.Fields)
            {
                var propType = FindType(cx, f.Type);
                var (prop, field) = type.AddAutoProperty(f.Name, propType);
                props.Add(f.Name, (prop, field));
            }

            type.AddCreateConstructor(cx, props.Select(k => (k.Key, k.Value.field)).ToArray());

            type.ImplementEquality(type.GetProperties().ToArray());
        }

        private void GenerateUnion(EmitContext cx, VirtualType type, TypeDefCore.UnionCase union)
        {
            type.Methods.Add(new VirtualMethod(type, Accessibility.ProtectedAndInternal, "Seal", new IParameter[0], cx.FindType(typeof(void)), isAbstract: true));
            type.ImplementEqualityForBase();
            // var cases = new Dictionary<string, IType>();
            foreach (var c in union.Options)
            {
                var valueType = FindType(cx, c);
                string name(TypeRef t) =>
                    t.Match(
                        actual: x => x.TypeName,
                        nullable: x => name(x.Type),
                        list: x => name(x.Type) + "List"
                    );
                var caseName = name(c);

                var caseType = new VirtualType(TypeKind.Class, Accessibility.Public,
                    type.FullTypeName.NestedType(SymbolNamer.NameMember(type, caseName + "Case", lowerCase: false), 0),
                    isStatic: false,
                    isSealed: true,
                    isAbstract: false,
                    declaringType: type
                );
                caseType.DirectBaseType = type;
                // caseType.ImplementedInterfaces
                type.NestedTypes.Add(caseType);

                var sealMethod = new VirtualMethod(caseType, Accessibility.ProtectedAndInternal, "Seal", new IParameter[0], cx.FindType(typeof(void)), isOverride: true);
                sealMethod.BodyFactory = () => EmitExtensions.CreateOneBlockFunction(sealMethod);
                caseType.Methods.Add(sealMethod);

                var valueProperty = caseType.AddAutoProperty("Item", valueType);
                var caseCtor = caseType.AddCreateConstructor(cx, new [] { ("item", valueProperty.field) });

                caseType.ImplementEqualityForCase(type, valueProperty.prop);


                var caseFactory = new VirtualMethod(type, Accessibility.Public,
                    SymbolNamer.NameMethod(type, caseName, 0, new IType[] { valueType }),
                    new [] { new DefaultParameter(valueType, "item") },
                    returnType: type,
                    isStatic: true
                );
                caseFactory.BodyFactory = () =>
                    EmitExtensions.CreateExpressionFunction(caseFactory,
                        new IL.NewObj(caseCtor) { Arguments = { new IL.LdLoc(new IL.ILVariable(VariableKind.Parameter, valueType, 0)) } }
                    );
                type.Methods.Add(caseFactory);
            }
        }

        public string Build(DataSchema schema, EmitSettings settings)
        {
            var cx = new EmitContext(
                new HackedSimpleCompilation(
                    new VirtualModuleReference(true, "NewEpicModule"),
                    ReferencedModules.Value
                ),
                settings
            );

            var types = schema.Types.ToDictionary(t => (settings.Namespace, t.Name), t => AddType(cx, t));
            cx.GeneratedTypes = types;

            foreach(var t in schema.Types)
                BuildType(cx, t);

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
        public EmitContext(HackedSimpleCompilation hackedSimpleCompilation, EmitSettings settings)
        {
            HackedSimpleCompilation = hackedSimpleCompilation;
            Module = (VirtualModule)Compilation.MainModule;
            Settings = settings;
        }

        public HackedSimpleCompilation HackedSimpleCompilation { get; }

        public VirtualModule Module { get; }

        public ICompilation Compilation => HackedSimpleCompilation;

        public EmitSettings Settings { get; }
        public Dictionary<(string @namespace, string name), VirtualType> GeneratedTypes { get; set; }

        public IType FindType(Type t) => Compilation.FindType(t);
        public IType FindType<T>() => Compilation.FindType(typeof(T));
        public IType FindType(FullTypeName name) => new GetClassTypeReference(name).Resolve(new SimpleTypeResolveContext(Compilation));
        public IMethod FindMethod<TResult>(Expression<Func<TResult>> expr)
        {
            var body = expr.Body;
            var methodInfo = body is MethodCallExpression call ? call.Method :
                                body is NewExpression @new        ? (MethodBase)@new.Constructor :
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
