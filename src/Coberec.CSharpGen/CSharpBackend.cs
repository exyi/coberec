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
using Coberec.ExprCS.Helpers;

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

        E.TypeReference FindType(string name) =>
            this.typeSignatures.TryGetValue(name, out var propType) ? new E.SpecializedType(propType) :
            cx.Settings.PrimitiveTypeMapping.TryGetValue(name, out var fullName) ? cx.Metadata.FindType(fullName) :
            // throw new Exception($"Could not resolve type '{name}'");
            cx.Metadata.FindType(typeof(string));

        E.TypeReference FindType(TypeRef type) =>
            type.Match(a => FindType(a.TypeName),
                       n =>
                       {
                           var t = FindType(n.Type);
                           if (t.IsReferenceType == false) return new E.SpecializedType(cx.Metadata.FindTypeDef(typeof(Nullable<>)), new[] { t });
                           else return t;
                       },
                       l =>
                       {
                           var t = FindType(l.Type);
                           return new E.SpecializedType(cx.Metadata.FindTypeDef(typeof(ImmutableArray<>)), new[] { t });
                       });

        (E.TypeDef type, TypeSymbolNameMapping mapping) BuildType(TypeDef def)
        {
            if (this.realizedTypes.TryGetValue(def.Name, out var result))
                return result;

            var type = this.typeSignatures[def.Name];

            void handleError(ValidationErrorException ex)
            {
                var typeIndex = cx.FullSchema.Types.IndexOf(def);
                throw ex.Nest("core").Nest(typeIndex.ToString()).Nest("types");
            }

            try
            {
                result = def.Core.Match(
                    composite: composite => GenerateComposite(type, composite, def),
                    primitive: primitive => GenerateScalar(type, primitive, def),
                    union: union => GenerateUnion(type, union, def),
                    @interface: ifc => GenerateInterface(type, ifc, def));
            }
            catch (ValidationErrorException ex)
            {
                handleError(ex);
            }
            this.cx.Metadata.AddType(result.type, ex => {
                if (ex is ValidationErrorException vex)
                    handleError(vex);
                else throw ex;
            });


            this.realizedTypes.Add(def.Name, result);
            return result;
        }

        private (E.TypeDef, TypeSymbolNameMapping) GenerateInterface(E.TypeSignature type, TypeDefCore.InterfaceCase ifc, TypeDef typeDef)
        {
            var specialSymbols = new Dictionary<string, string>();
            var propDictionary = new Dictionary<string, (TypeField schema, E.PropertySignature prop)>();
            var props = new List<(TypeField schema, E.PropertySignature prop)>();

            foreach (var f in ifc.Fields)
            {
                var propType = FindType(f.Type);
                var prop = E.PropertySignature.Create(f.Name, type, propType, E.Accessibility.APublic, null);
                propDictionary.Add(f.Name, (f, prop));
                props.Add((f, prop));
            }

            var result = E.TypeDef.Empty(type)
                         .AddMember(props.Select(p => E.PropertyDef.InterfaceDef(p.prop)).ToArray());

            if (cx.Settings.EmitInterfaceWithMethods)
            {
                var withMethod = WithMethodImplementation.InterfaceWithMethod(
                    type,
                    props.Select(p => (p.prop.Type, p.schema.Name)).ToArray(),
                    isOptional: cx.Settings.EmitOptionalWithMethods && props.Count > 1,
                    returnValidationResult: cx.Settings.WithMethodReturnValidationResult
                );

                result = result.AddMember(withMethod);

                specialSymbols.Add("With", withMethod.Signature.Name);
            }

            var typeMapping = new TypeSymbolNameMapping(
                type.Name,
                props.ToDictionary(p => p.schema.Name, p => p.prop.Name),
                specialSymbols: specialSymbols
            );
            return (result, typeMapping);
        }

        private (E.TypeDef, TypeSymbolNameMapping) GenerateScalar(E.TypeSignature type, TypeDefCore.PrimitiveCase primitive, TypeDef typeDef)
        {
            var result = E.TypeDef.Empty(type);
            var (valueField, valueProperty) = PropertyBuilders.CreateAutoProperty(type, "Value", new E.SpecializedType(E.TypeSignature.String));
            var typeMapping = new TypeSymbolNameMapping(type.Name, new Dictionary<string, string> {
                ["value"] = valueProperty.Signature.Name
            });

            result = result.AddMember(valueField, valueProperty);

            cx.Metadata.RegisterTypeMod(type, _ => { }, vtype => {

                var (noValCtor, publicCtor, validateMethod) = vtype.AddObjectCreationStuff(
                    cx,
                    typeDef,
                    typeMapping,
                    new[] { ("value", E.MetadataDefiner.GetField(cx.Metadata, valueField.Signature) ) },
                    this.GetValidators(typeDef),
                    needsNoValidationConstructor: true);
                vtype.AddCreateFunction(cx, validateMethod, noValCtor);
                vtype.ImplementEquality(new[] { E.MetadataDefiner.GetProperty(cx.Metadata, valueProperty.Signature) });
            });

            return (result, typeMapping);
        }

        private (E.TypeDef, TypeSymbolNameMapping) GenerateComposite(E.TypeSignature type, TypeDefCore.CompositeCase composite, TypeDef typeDef)
        {
            var propDictionary = new Dictionary<string, (TypeField schema, E.PropertyDef prop, E.FieldDef field)>();
            var props = new List<(TypeField schema, E.PropertyDef prop, E.FieldDef field)>();

            foreach (var f in composite.Fields)
            {
                var propType = FindType(f.Type);
                var (field, prop) = PropertyBuilders.CreateAutoProperty(type, f.Name, propType);
                propDictionary.Add(f.Name, (f, prop, field));
                props.Add((f, prop, field));
            }

            var typeMapping = new TypeSymbolNameMapping(
                type.Name,
                props.ToDictionary(p => p.schema.Name, p => p.prop.Signature.Name)
            );


            var interfaces =
                from f in composite.Implements
                let name = ((TypeRef.ActualTypeCase)f).TypeName
                let declaration = this.typeSchemas[name]
                let definition = this.BuildType(declaration)
                select (declaration, definition.type, definition.mapping);

            var result = E.TypeDef.Empty(type)
                         .With(implements: interfaces.Select(i => new E.SpecializedType(i.type.Signature)).ToImmutableArray())
                         .AddMember(props.Select(p => p.prop).ToArray())
                         .AddMember(props.Select(p => p.field).ToArray());

            cx.Metadata.RegisterTypeMod(type, _ => { }, vtype => {

                var (noValCtor, publicCtor, validateMethod) = vtype.AddObjectCreationStuff(
                    cx,
                    typeDef,
                    typeMapping,
                    props.Select(k => (k.schema.Name, E.MetadataDefiner.GetField(cx.Metadata, k.field.Signature))).ToArray(),
                    this.GetValidators(typeDef),
                    needsNoValidationConstructor: true);

                if (cx.Settings.AddJsonPropertyAttributes)
                {
                    foreach (var (f, p, _) in props)
                        JsonSerialializationHelpers.AddPropertyAttributes((VirtualProperty)E.MetadataDefiner.GetProperty(cx.Metadata, p.Signature), f.Name);
                    JsonSerialializationHelpers.AddParameterAttributes(publicCtor, props.Select(p => p.schema.Name));
                }

                var createFn = vtype.AddCreateFunction(cx, validateMethod, noValCtor);

                var properties = props.Select(p => E.MetadataDefiner.GetProperty(cx.Metadata, p.prop.Signature)).ToArray();

                vtype.ImplementEquality(properties);

                IMethod withMethod = null;
                if (cx.Settings.EmitWithMethods)
                {
                    withMethod = vtype.ImplementWithMethod(cx.Settings.WithMethodReturnValidationResult ? createFn : publicCtor, properties, cx.Settings.WithMethodReturnValidationResult);
                    if (cx.Settings.EmitOptionalWithMethods && properties.Length > 1)
                        vtype.ImplementOptionalWithMethod(withMethod, properties);
                }

                foreach (var i in interfaces)
                {
                    var interfaceFields =
                    (from ifcProp in ((TypeDefCore.InterfaceCase)i.declaration.Core).Fields
                        select (
                            ifcProp,
                            myProp: propDictionary[ifcProp.Name],
                            ifcRealProp: i.mapping.Fields[ifcProp.Name]
                                        .Apply(n => i.type.Members.Where(p => p.Signature.Name == n))
                                        .Cast<E.PropertyDef>()
                                        .Single()
                        )
                    ).ToArray();

                    foreach (var (ifcProp, myProp, ifcRealProp) in interfaceFields)
                    {
                        if (myProp.prop == null ||
                            myProp.prop.Signature.Name != ifcRealProp.Signature.Name || // TODO: ASAP migrate to ExprCS. This logic should disappear
                            myProp.prop.Signature.Type != ifcRealProp.Signature.Type)
                            // add explicit implementation if needed
                            vtype.AddExplicitInterfaceProperty(E.MetadataDefiner.GetProperty(cx.Metadata, ifcRealProp.Signature), E.MetadataDefiner.GetProperty(cx.Metadata, myProp.prop.Signature));
                    }

                    if (i.mapping.GetSymbol("With") is string withMethodName)
                    {
                        var ifcWithMethod = i.type.Members.OfType<E.MethodDef>().Where(m => m.Signature.Name == withMethodName).Single();

                        if (withMethod == null) throw new NotSupportedException($"Could not implement {i.declaration.Name} for {type.Name} due to conflict in With method settings.");

                        vtype.InterfaceImplementationWithMethod(
                            withMethod,
                            E.MetadataDefiner.GetMethod(cx.Metadata, ifcWithMethod.Signature),
                            interfaceFields.Select(x =>
                                (E.MetadataDefiner.GetProperty(cx.Metadata, x.myProp.prop.Signature) as IMember, x.ifcProp.Name)).ToArray(),
                            properties
                        );
                    }
                }

            });

            return (result, typeMapping);
        }

        private (E.TypeDef, TypeSymbolNameMapping) GenerateUnion(E.TypeSignature type, TypeDefCore.UnionCase union, TypeDef typeDef)
        {
            // var sealMethodName = SymbolNamer.NameMethod(type, "Seal", 0, new IType[0]);
            // type.Methods.Add(new VirtualMethod(type, Accessibility.ProtectedAndInternal, sealMethodName, new IParameter[0], cx.FindType(typeof(void)), isAbstract: true));

            var caseTypes = new Dictionary<string, string>();
            var result = E.TypeDef.Empty(type);

            cx.Metadata.RegisterTypeMod(type, _ => { }, vtype => {

                var (abstractEqCore, _) = vtype.ImplementEqualityForBase();

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
                        vtype.FullTypeName.NestedType(E.SymbolNamer.NameMember(vtype, caseName + "Case", lowerCase: false), 0),
                        isStatic: false,
                        isSealed: true,
                        isAbstract: false,
                        declaringType: vtype
                    );
                    caseType.DirectBaseType = vtype;
                    vtype.NestedTypes.Add(caseType);
                    return (index, schema, caseName, caseType);
                }).ToArray();

                var baseMatch = vtype.ImplementMatchBase(cases.Select(c => ((IType)c.caseType, c.caseName)).ToArray());

                var caseCtors = new List<IMethod>();

                foreach (var (index, schema, caseName, caseType) in cases)
                {
                    var valueType = FindType(schema);

                    // var sealMethod = new VirtualMethod(caseType, Accessibility.ProtectedAndInternal, sealMethodName, new IParameter[0], cx.FindType(typeof(void)), isOverride: true);
                    // sealMethod.BodyFactory = () => EmitExtensions.CreateOneBlockFunction(sealMethod);
                    // caseType.Methods.Add(sealMethod);

                    var valueProperty = caseType.AddAutoProperty("Item", E.MetadataDefiner.GetTypeReference(cx.Metadata, valueType));
                    var caseCtor = caseType.AddCreateConstructor(cx, new[] { ("item", valueProperty.field) }, false);
                    caseCtors.Add(caseCtor);

                    caseType.ImplementEqualityForCase(abstractEqCore, valueProperty.prop);
                    caseType.ImplementMatchCase(baseMatch, index);

                    vtype.ImplementBasicCaseFactory(caseName, caseCtor);
                    vtype.TryImplementForwardingCaseFactory(caseName, caseCtor); // TODO: configurable

                    caseTypes.Add(caseName, caseType.Name);
                }

                vtype.ImplementAllIntoCaseConversions(caseCtors.ToArray()); // TODO: configurable (if, if implicit)
            });

            return (result, new TypeSymbolNameMapping(
                type.Name,
                caseTypes
            ));
        }

        private ValidatorUsage[] GetValidators(TypeDef type, string unionCase = null)
        {
            return type.GetValidatorsForType(n => cx.Settings.Validators.GetValueOrDefault(n)?.ValidatorParameters.Select(p => p.name).ToArray()).ToArray();
        }

        private void InitializeExternalSymbols()
        {
            // TODO: move to ExprCS
            IType findType(string name)
            {
                if (this.typeSignatures.TryGetValue(name, out var result))
                    throw new NotSupportedException(); //return result;
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
            new E.EmitSettings(settings.EmitPartialClasses, adjustCasing: true);


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
