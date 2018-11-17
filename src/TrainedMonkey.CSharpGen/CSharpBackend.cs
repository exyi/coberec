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

            var type = new VirtualType(
                TypeKind.Class,
                Accessibility.Public,
                new FullTypeName(new TopLevelTypeName(cx.Settings.Namespace, name)),
                isStatic: false,
                isSealed: true,
                isAbstract: false,
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

            // var tString = cx.FindType<string>();
            // var tChar = compilation.FindType(typeof(char));
            // var t2 = compilation.FindType(typeof(IEnumerable<char>));
            // var tEnumerable = compilation.FindType(typeof(Enumerable));
            // var toArray = tEnumerable.GetMethods(m => m.Name == "ToArray").Single().Specialize(new TypeParameterSubstitution(null, new[] { tChar }));

            // var adhocType = new VirtualType(TypeKind.Class, Accessibility.Public, new FullTypeName("SomeNs.SomeType"), isStatic: false, isSealed: false, isAbstract: false, parentModule: mod);
            // mod.AddType(adhocType);
            // var methodParams = new[] { new DefaultParameter(tString, "testParam") };
            // var adhocMethod = new VirtualMethod(adhocType, Accessibility.Public, "SomeMethod", methodParams, t2);
            // adhocType.Methods.Add(adhocMethod);

            // // var method = tString.GetMethods(m => m.IsConstructor && m.Parameters.Count == 1 && m.Parameters.Single().Type.Name == "IEnumerable`1").Single();

            // adhocMethod.BodyFactory = () =>
            // {
            //     var variable = new ILVariable(VariableKind.Local, tString, StackType.O, 0);
            //     var functionContainer = new BlockContainer(expectedResultType: StackType.O);
            //     functionContainer.Blocks.Add(
            //         new Block()
            //         {
            //             Instructions = {
            //                 new IL.StLoc(variable, new IL.LdStr("ahoj\"\u200BF")),
            //                 new IL.Leave(functionContainer, value: new IL.Call(toArray) { Arguments = { new IL.LdLoc(variable) } })
            //             },
            //             // FinalInstruction = new IL.LdLoc(variable)
            //         });

            //     var ilFunc = new ILFunction(adhocMethod, 10000, new ICSharpCode.Decompiler.TypeSystem.GenericContext(), functionContainer)
            //     {
            //         Variables = { variable }
            //     };
            //     ilFunc.AddRef(); // whatever, somehow initializes the freaking tree
            //     Debug.Assert(variable.Function == ilFunc);
            //     return ilFunc;
            // };

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

        private static Lazy<PEFile[]> ReferencedModules = new Lazy<PEFile[]>(() => GetReferencedPaths().Select(a => new PEFile(a)).ToArray());
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
