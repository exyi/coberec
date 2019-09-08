using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using ICSharpCode.Decompiler.IL;
using IL = ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using Coberec.CSharpGen.TypeSystem;
using System.Diagnostics;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler;
using Coberec.CSharpGen.Emit;
using System.Linq.Expressions;
using System.Reflection;
using System.Globalization;
using System.Text;
using Coberec.MetaSchema;
using System.Collections.Immutable;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using Coberec.CoreLib;
using System.Reflection.PortableExecutable;
using E=Coberec.ExprCS;

namespace Coberec.CSharpGen
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
        private readonly E.NamespaceSignature @namespace;
        private Dictionary<string, TypeDef> typeSchemas;
        private Dictionary<string, E.TypeSignature> typeSignatures;
        private Dictionary<string, (E.TypeDef type, TypeSymbolNameMapping mapping)> realizedTypes = new Dictionary<string, (E.TypeDef, TypeSymbolNameMapping)>();
        private CSharpBackend(EmitContext cx)
        {
            this.cx = cx;
            this.@namespace = E.NamespaceSignature.Parse(cx.Settings.Namespace);
        }
        E.TypeSignature CreateTypeSignature(EmitContext cx, TypeDef def)
        {
            var isAbstract = def.Core is TypeDefCore.UnionCase;
            var typeKind = def.Core is TypeDefCore.InterfaceCase ? "interface" :
                           "class";

            var type = new E.TypeSignature(
                def.Name,
                this.@namespace,
                typeKind,
                isValueType: false,
                canOverride: isAbstract || typeKind == "interface",
                isAbstract: isAbstract,
                E.Accessibility.APublic,
                genericParamCount: 0
            );
            return type;
        }

        IType FindType(string name) =>
            this.typeSignatures.TryGetValue(name, out var propType) ? (IType)propType :
            cx.Settings.PrimitiveTypeMapping.TryGetValue(name, out var fullName) ? cx.FindType(new FullTypeName(fullName)) :
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

        (E.TypeDef, TypeSymbolNameMapping) BuildType(TypeDef def)
        {
            if (this.realizedTypes.TryGetValue(def.Name, out var result))
                return result;

            var type = this.typeSignatures[def.Name];

            try
            {
                var mapping = def.Core.Match(
                    composite: composite => GenerateComposite(type, composite, def),
                    primitive: primitive => GenerateScalar(type, primitive, def),
                    union: union => GenerateUnion(type, union, def),
                    @interface: ifc => GenerateInterface(type, ifc, def));
                result = (type, mapping);
            }
            catch (ValidationErrorException ex)
            {
                var typeIndex = cx.FullSchema.Types.IndexOf(def);
                throw ex.Nest("core").Nest(typeIndex.ToString()).Nest("types");
            }

            this.realizedTypes.Add(def.Name, result);
            return result;
        }

        private (E.TypeDef, TypeSymbolNameMapping) GenerateInterface(E.TypeSignature type, TypeDefCore.InterfaceCase ifc, TypeDef typeDef)
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
                var withMethod = type.InterfaceWithMethod(
                    props.Select(p => (p.prop as IMember, p.schema.Name)).ToArray(),
                    isOptional: cx.Settings.EmitOptionalWithMethods && props.Count > 1,
                    returnValidationResult: cx.Settings.WithMethodReturnValidationResult
                );

                specialSymbols.Add("With", withMethod.Name);
            }

            return new TypeSymbolNameMapping(
                type.Name,
                props.ToDictionary(p => p.schema.Name, p => p.prop.Name),
                specialSymbols: specialSymbols
            );
        }

        private TypeSymbolNameMapping GenerateScalar(VirtualType type, TypeDefCore.PrimitiveCase primitive, TypeDef typeDef)
        {
            var valueProperty = type.AddAutoProperty("Value", cx.FindType<string>());
            var typeMapping = new TypeSymbolNameMapping(type.Name, new Dictionary<string, string> {
                ["value"] = valueProperty.prop.Name
            });

            var (noValCtor, publicCtor, validateMethod) = type.AddObjectCreationStuff(
                cx,
                typeDef,
                typeMapping,
                new[] { ("value", valueProperty.field) },
                this.GetValidators(typeDef),
                needsNoValidationConstructor: true);
            type.AddCreateFunction(cx, validateMethod, noValCtor);
            type.ImplementEquality(new[] { valueProperty.prop });

            return typeMapping;
        }

        private TypeSymbolNameMapping GenerateComposite(VirtualType type, TypeDefCore.CompositeCase composite, TypeDef typeDef)
        {
            var propDictionary = new Dictionary<string, (TypeField schema, IProperty prop, IField field)>();
            var props = new List<(TypeField schema, VirtualProperty prop, IField field)>();

            foreach (var f in composite.Fields)
            {
                var propType = FindType(f.Type);
                var (prop, field) = type.AddAutoProperty(f.Name, propType);
                propDictionary.Add(f.Name, (f, prop, field));
                props.Add((f, prop, field));
            }

            var typeMapping = new TypeSymbolNameMapping(
                type.Name,
                props.ToDictionary(p => p.schema.Name, p => p.prop.Name)
            );

            var (noValCtor, publicCtor, validateMethod) = type.AddObjectCreationStuff(
                cx,
                typeDef,
                typeMapping,
                props.Select(k => (k.schema.Name, k.field)).ToArray(),
                this.GetValidators(typeDef),
                needsNoValidationConstructor: true);

            if (cx.Settings.AddJsonPropertyAttributes)
            {
                foreach (var (f, p, _) in props)
                    JsonSerialializationHelpers.AddPropertyAttributes(p, f.Name);
                JsonSerialializationHelpers.AddParameterAttributes(publicCtor, props.Select(p => p.schema.Name));
            }

            var createFn = type.AddCreateFunction(cx, validateMethod, noValCtor);

            var properties = props.Select(p => p.prop).ToArray();

            type.ImplementEquality(properties);

            IMethod withMethod = null;
            if (cx.Settings.EmitWithMethods)
            {
                withMethod = type.ImplementWithMethod(cx.Settings.WithMethodReturnValidationResult ? createFn : publicCtor, properties, cx.Settings.WithMethodReturnValidationResult);
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

            return typeMapping;
        }

        private TypeSymbolNameMapping GenerateUnion(VirtualType type, TypeDefCore.UnionCase union, TypeDef typeDef)
        {
            // var sealMethodName = SymbolNamer.NameMethod(type, "Seal", 0, new IType[0]);
            // type.Methods.Add(new VirtualMethod(type, Accessibility.ProtectedAndInternal, sealMethodName, new IParameter[0], cx.FindType(typeof(void)), isAbstract: true));
            var (abstractEqCore, _) = type.ImplementEqualityForBase();

            var cases = union.Options.Select((schema, index) =>
            {
                string name(TypeRef t) =>
                    t.Match(
                        actual: x => x.TypeName.EndsWith(typeDef.Name) ? x.TypeName.Remove(x.TypeName.Length - typeDef.Name.Length) :
                                     x.TypeName, // TODO: document behavior
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

            var caseCtors = new List<IMethod>();

            foreach (var (index, schema, caseName, caseType) in cases)
            {
                var valueType = FindType(schema);

                // var sealMethod = new VirtualMethod(caseType, Accessibility.ProtectedAndInternal, sealMethodName, new IParameter[0], cx.FindType(typeof(void)), isOverride: true);
                // sealMethod.BodyFactory = () => EmitExtensions.CreateOneBlockFunction(sealMethod);
                // caseType.Methods.Add(sealMethod);

                var valueProperty = caseType.AddAutoProperty("Item", valueType);
                var caseCtor = caseType.AddCreateConstructor(cx, new[] { ("item", valueProperty.field) }, false);
                caseCtors.Add(caseCtor);

                caseType.ImplementEqualityForCase(abstractEqCore, valueProperty.prop);
                caseType.ImplementMatchCase(baseMatch, index);

                type.ImplementBasicCaseFactory(caseName, caseCtor);
                type.TryImplementForwardingCaseFactory(caseName, caseCtor); // TODO: configurable
            }

            type.ImplementAllIntoCaseConversions(caseCtors.ToArray()); // TODO: configurable (if, if implicit)

            return new TypeSymbolNameMapping(
                type.Name,
                cases.ToDictionary(c => c.caseName, c => c.caseType.Name)
            );
        }

        private ValidatorUsage[] GetValidators(TypeDef type, string unionCase = null)
        {
            return type.GetValidatorsForType(n => cx.Settings.Validators.GetValueOrDefault(n)?.ValidatorParameters.Select(p => p.name).ToArray()).ToArray();
        }

        private void InitializeExternalSymbols()
        {
            IType findType(string name)
            {
                if (this.typeSignatures.TryGetValue(name, out var result))
                    return result;
                return cx.FindType(new FullTypeName(name));
            }
            foreach (var s in cx.Settings.ExternalSymbols)
            {
                switch (s.Kind) {
                    case ExternalSymbolKind.TypeDefinition: {
                        // TODO: warning when in the same namespace as model
                        //       error in case of real name collision
                        var newType = new VirtualType(TypeKind.Class, Accessibility.Public, new FullTypeName(new TopLevelTypeName(s.DeclaredIn, s.Name)), isStatic: false, isSealed: false, isAbstract: false, parentModule: cx.Module, isHidden: true);
                        cx.Metadata.AddRawType(newType);
                        break;
                    }
                    case ExternalSymbolKind.Method:
                    case ExternalSymbolKind.StaticMethod: {
                        var methodArgs = s.Args.Select(a => new VirtualParameter(findType(a.Type), a.Name)).ToArray();
                        var declaringType = (VirtualType)findType(s.DeclaredIn);
                        var newMethod = new VirtualMethod(declaringType, Accessibility.Public, s.Name, methodArgs, findType(s.ResultType), isStatic: s.Kind == ExternalSymbolKind.StaticMethod, isHidden: true);
                        declaringType.Methods.Add(newMethod);
                        break;
                    }
                    default:
                        throw new NotSupportedException($"External symbols of kind {s.Kind} are not supported.");
                }
            }

        }

        public static E.EmitSettings GetEmitSettings(EmitSettings settings) =>
            new E.EmitSettings(settings.EmitPartialClasses);


        public static string Build(DataSchema schema, EmitSettings settings)
        {
            var cx = BuildCore(schema, settings);
            return cx.EmitToString();
        }

        public static IEnumerable<string> BuildIntoFolder(DataSchema schema, EmitSettings settings, string targetDir)
        {
            var cx = BuildCore(schema, settings);
            return cx.EmitToDirectory(targetDir);
        }

        public ValidationErrors ValidateSchema()
        {
            return cx.FullSchema.ValidateTypeReferences(predefinedTypes: cx.Settings.PrimitiveTypeMapping.Keys);
        }

        private static E.MetadataContext BuildCore(DataSchema schema, EmitSettings settings)
        {
            var cx2 = E.MetadataContext.Create("NewEpicModule", settings.AdditionalReferences, GetEmitSettings(settings));
            var cx = new EmitContext(cx2, settings, schema);

            var @this = new CSharpBackend(cx);

            @this.typeSchemas = schema.Types.ToDictionary(t => t.Name);

            @this.typeSignatures = schema.Types.ToDictionary(t => t.Name, t => @this.CreateTypeSignature(cx, t));

            @this.InitializeExternalSymbols();

            if (!cx.Settings.FallbackToStringType)
                @this.ValidateSchema().ThrowErrors("Schema validation has failed");

            var symbolNameMapping = new Dictionary<string, TypeSymbolNameMapping>();

            foreach (var t in schema.Types)
            {
                var (type, mapping) = @this.BuildType(t);
                symbolNameMapping.Add(t.Name, mapping);
            }

            return cx2;
        }
    }
}
