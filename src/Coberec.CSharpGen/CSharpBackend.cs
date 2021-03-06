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
                typeParameters: ImmutableArray<E.GenericParameter>.Empty
            );
            return type;
        }

        E.TypeReference FindType(string name) =>
            this.typeSignatures.TryGetValue(name, out var propType) ? new E.SpecializedType(propType) :
            cx.Settings.PrimitiveTypeMapping.TryGetValue(name, out var fullName) ? cx.Metadata.FindType(fullName) :
            // throw new Exception($"Could not resolve type '{name}'");
            E.TypeSignature.String;

        E.TypeReference FindType(TypeRef type) =>
            type.Match(a => FindType(a.TypeName),
                       n =>
                       {
                           var t = FindType(n.Type);
                           if (t.IsReferenceType == false) return E.TypeSignature.NullableOfT.Specialize(t);
                           else return t;
                       },
                       l =>
                       {
                           var t = FindType(l.Type);
                           return new E.SpecializedType(E.TypeSignature.FromType(typeof(ImmutableArray<>)), new[] { t });
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
                if (def.Description is object)
                    result.type = result.type.With(doccomment: SummaryComment(def.Description));
            }
            catch (ValidationErrorException ex)
            {
                handleError(ex);
            }
            this.cx.Metadata.AddType(result.type, ex => {
                if (ex is ValidationErrorException vex)
                {
                    handleError(vex);
                    return true;
                }
                else
                {
                    return false;
                }
            });


            this.realizedTypes.Add(def.Name, result);
            return result;
        }

        static E.XmlComment SummaryComment(string description) =>
            description is object ?
            new E.XmlComment($"<summary> {description} </summary>") :
            null;

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
                         .AddMember(props.Select(p => E.PropertyDef.InterfaceDef(p.prop, SummaryComment(p.schema.Description))).ToArray());

            if (cx.Settings.EmitInterfaceWithMethods)
            {
                var withMethod = WithMethodImplementation.InterfaceWithMethod(
                    type,
                    props.Select(p => (p.prop.Type, p.schema.Name)).ToArray(),
                    typeDef,
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
            var (valueField, valueProperty) = E.PropertyBuilders.CreateAutoProperty(type, "Value", new E.SpecializedType(E.TypeSignature.String));
            var typeMapping = new TypeSymbolNameMapping(type.Name, new Dictionary<string, string> {
                ["value"] = valueProperty.Signature.Name
            });

            result = result.AddMember(valueField, valueProperty);

            var valExtension = AddValidationExtension(type);

            var (noValCtor, publicCtor, bCtor, validateMethod, _) = type.AddObjectCreationStuff(
                cx,
                typeDef,
                new[] { (new TypeField("value", TypeRef.ActualType("String"), null, new Directive[0]), valueField.Signature.SpecializeFromDeclaringType() ) },
                this.GetValidators(typeDef),
                needsNoValidationConstructor: true,
                validateMethodExtension: valExtension?.Signature);

            var (createFn, _) = type.AddCreateFunction(cx, validateMethod?.Signature, noValCtor.Signature, null, typeDef);

            cx.Metadata.RegisterTypeMod(type, _ => { }, vtype => {

                vtype.ImplementEquality(new[] { E.MetadataDefiner.GetProperty(cx.Metadata, valueProperty.Signature) });
            });

            result = result.AddMember(noValCtor, publicCtor, bCtor, validateMethod, createFn, valExtension);

            return (result, typeMapping);
        }

        private E.MethodDef AddValidationExtension(E.TypeSignature type)
        {
            if (!cx.Settings.EmitValidationExtension)
                return null;
            var s = E.MethodSignature.Static("ValidateObjectExtension", type, E.Accessibility.APrivate, E.TypeSignature.Void, new E.MethodParameter(E.TypeReference.ByReferenceType(E.TypeReference.FromType(typeof(ValidationErrorsBuilder))), "e"), new E.MethodParameter(type, "obj"));

            cx.Metadata.RegisterTypeMod(type, _ => {}, vtype => {
                var a = (VirtualMethod)vtype.Methods.Single(m => m.Name == s.Name);
                a.BodyFactory = null;
                a.IsPartial = true;
            });

            return E.MethodDef.CreateWithArray(s, _ => E.Expression.Nop);
        }

        private (E.TypeDef, TypeSymbolNameMapping) GenerateComposite(E.TypeSignature type, TypeDefCore.CompositeCase composite, TypeDef typeDef)
        {
            var propDictionary = new Dictionary<string, (TypeField schema, E.PropertyDef prop, E.FieldDef field)>();
            var props = new List<(TypeField schema, E.PropertyDef prop, E.FieldDef field)>();

            foreach (var f in composite.Fields)
            {
                var propType = FindType(f.Type);
                var (field, prop) = E.PropertyBuilders.CreateAutoProperty(type, f.Name, propType, doccomment: SummaryComment(f.Description));

                // explicitly implement the interface
                foreach (var i in composite.Implements)
                {
                    var iname = ((TypeRef.ActualTypeCase)i).TypeName;
                    var idef = cx.FullSchema.Types.Single(t => t.Name == iname && t.Core is TypeDefCore.InterfaceCase);
                    var matchingProp = ((TypeDefCore.InterfaceCase)idef.Core).Fields.SingleOrDefault(ff => ff.Name == f.Name);
                    if (matchingProp is null) continue;

                    var (ifc_type, ifc_mapping) = BuildType(idef);
                    var iprop = ifc_type.Members.OfType<E.PropertyDef>().Single(p => p.Signature.Name == ifc_mapping.Fields[f.Name]);
                    prop = prop.AddImplements(iprop.Signature);
                }

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

            var valExtension = AddValidationExtension(type);

            var fields = props.Select(k => (k.schema, k.field.Signature.SpecializeFromDeclaringType())).ToArray();
            var (noValCtor, publicCtor, benevolentCtor, validateMethod, defaults) = type.AddObjectCreationStuff(
                cx,
                typeDef,
                fields,
                this.GetValidators(typeDef),
                needsNoValidationConstructor: true,
                validateMethodExtension: valExtension?.Signature);

            var (createFn, benevolentCreate) = type.AddCreateFunction(cx, validateMethod?.Signature, noValCtor.Signature, default, typeDef);

            result = result.AddMember(noValCtor, (object)publicCtor == noValCtor ? null : publicCtor, benevolentCtor, validateMethod, createFn, benevolentCreate, valExtension);

            var formatFn = ToStringImplementation.ImplementFormat(type, typeDef, fields);
            var toStringFn = ToStringImplementation.ImplementToString(type);
            result = result.AddMember(toStringFn, formatFn)
                .AddImplements(E.TypeSignature.FromType(typeof(ITokenFormatable)).Specialize());

            result =
                TraversableObjectImplementation
                .ImplementTraversable(type, fields)
                .Invoke(result);

            cx.Metadata.RegisterTypeMod(type, _ => { }, vtype => {

                if (formatFn != null && typeDef.Directives.Any(d => d.Name == "customFormat"))
                {
                    var x = (VirtualMethod)vtype.Methods.Single(m => m.Name == formatFn.Signature.Name && m.Parameters.Count == formatFn.Signature.Params.Length);
                    x.IsHidden = true;
                }

                if (cx.Settings.AddJsonPropertyAttributes)
                {
                    foreach (var (f, p, _) in props)
                        JsonSerialializationHelpers.AddPropertyAttributes((VirtualProperty)E.MetadataDefiner.GetProperty(cx.Metadata, p.Signature), f.Name);
                    JsonSerialializationHelpers.AddParameterAttributes(E.MetadataDefiner.GetMethod(cx.Metadata, publicCtor.Signature), props.Select(p => p.schema.Name));
                }

                var properties = props.Select(p => E.MetadataDefiner.GetProperty(cx.Metadata, p.prop.Signature)).ToArray();

                vtype.ImplementEquality(properties);

                IMethod withMethod = null;
                if (cx.Settings.EmitWithMethods)
                {
                    withMethod = vtype.ImplementWithMethod(E.MetadataDefiner.GetMethod(cx.Metadata, cx.Settings.WithMethodReturnValidationResult ? createFn.Signature : publicCtor.Signature), properties, typeDef, cx.Settings.WithMethodReturnValidationResult);
                    if (cx.Settings.EmitOptionalWithMethods && properties.Length > 1)
                        vtype.ImplementOptionalWithMethod(withMethod, properties, typeDef);
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

            string name(TypeRef t) =>
                t.Match(
                    actual: x => x.TypeName, // TODO: document behavior
                    nullable: x => name(x.Type),
                    list: x => name(x.Type) + "List"
                );
            TypeDef findLocalType(TypeRef t) =>
                t.Match(
                    actual: x => this.typeSchemas.TryGetValue(x.TypeName, out var t) ? t : null,
                    list: x => findLocalType(x),
                    nullable: x => findLocalType(x)
                );
            var names = union.Options.Select(name).ToArray();

            var cases = union.Options.Select((schema, index) => {
                var caseName = names[index];

                if (caseName.EndsWith(typeDef.Name))
                {
                    var trimmedName = caseName.Remove(caseName.Length - typeDef.Name.Length);
                    if (!names.Contains(trimmedName))
                        caseName = trimmedName;
                }

                var caseType = E.TypeSignature.SealedClass(
                    caseName + "Case",
                    type,
                    E.Accessibility.APublic
                );
                var valueType = FindType(schema);
                var (field, prop) = E.PropertyBuilders.CreateAutoProperty(caseType, "Item", valueType);
                var caseCtor = caseType.AddCreateConstructor(cx, new[] { ("item", field.Signature.SpecializeFromDeclaringType()) });

                var caseDescription = findLocalType(schema)?.Description;
                var def = E.TypeDef.Empty(caseType).With(
                    extends: type.SpecializeByItself(),
                    doccomment: SummaryComment(caseDescription)
                );
                def = def.AddMember(caseCtor, field, prop)
                         .AddMember(E.MethodDef.Create(
                    E.MethodSignature.Override(caseType, E.MethodSignature.Object_ToString),
                    @this => E.FluentExpression.CallMethod(E.FluentExpression.Box(E.FluentExpression.ReadProperty(@this, prop.Signature)), E.MethodSignature.Object_ToString)
                ));

                def = TraversableObjectImplementation.ImplementTraversableUnionCase(type, caseType, field.Signature, caseName)
                      .Invoke(def);

                return (index, schema, caseName, caseType: def, caseCtor);
            }).ToArray();

            var baseMatch = MatchFunctionImplementation.ImplementMatchBase(
                type, cases.Select(c => (c.caseType, c.caseName)).ToArray());
            result = result.AddMember(baseMatch);

            var abstractGetHashCode = E.MethodDef.InterfaceDef(E.MethodSignature.Override(type, E.MethodSignature.Object_GetHashCode, isAbstract: true));
            result = result.AddMember(abstractGetHashCode);

            for (int i = 0; i < cases.Length; i++)
            {
                var caseMatch = MatchFunctionImplementation.ImplementMatchCase(cases[i].caseType, baseMatch.Signature, i);
                cases[i].caseType = cases[i].caseType.AddMember(caseMatch);
            }

            result = result.AddMember(cases.Select(c => c.caseType).ToArray());
            result = TraversableObjectImplementation.ImplementTraversableUnion(result.Signature)
                     .Invoke(result);

            cx.Metadata.RegisterTypeMod(type, _ => { }, vtype => {
                var (abstractEqCore, _) = vtype.ImplementEqualityForBase();

                var caseCtors = new List<IMethod>();

                foreach (var (index, schema, caseName, caseType, caseCtor) in cases)
                {
                    var caseType_ = (VirtualType)E.MetadataDefiner.GetTypeReference(cx.Metadata, caseType.Signature);
                    var caseCtor_ = E.MetadataDefiner.GetMethod(cx.Metadata, caseCtor.Signature);

                    var valueProperty = caseType_.Properties.Single(p => !p.IsOverride);
                    caseCtors.Add(caseCtor_);

                    caseType_.ImplementEqualityForCase(abstractEqCore, valueProperty);

                    vtype.ImplementBasicCaseFactory(caseName, caseCtor_);
                    vtype.TryImplementForwardingCaseFactory(caseName, caseCtor_); // TODO: configurable

                    caseTypes.Add(caseName, caseType_.Name);
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
            E.TypeReference findType(string name)
            {
                if (this.typeSignatures.TryGetValue(name, out var result))
                    return result;
                return cx.Metadata.TryFindType(name);
            }
            var types = new Dictionary<string, E.TypeDef>();
            foreach (var s in cx.Settings.ExternalSymbols)
            {
                switch (s.Kind) {
                    case ExternalSymbolKind.TypeDefinition: {
                        var declaringType = cx.Metadata.TryFindTypeDef(s.DeclaredIn);
                        var declaredIn = declaringType is object ? E.TypeOrNamespace.TypeSignature(declaringType) : E.TypeOrNamespace.NamespaceSignature(E.NamespaceSignature.Parse(s.DeclaredIn));

                        var newType = E.TypeSignature.Class(s.Name, declaredIn, E.Accessibility.APublic);
                        types.Add(s.DeclaredIn + "." + s.Name, E.TypeDef.Empty(newType));
                        break;
                    }
                    case ExternalSymbolKind.Method:
                    case ExternalSymbolKind.StaticMethod: {
                        var declaringType = types.GetValueOrDefault(s.DeclaredIn) ??
                                            throw new Exception($"The declaring symbol `{s.DeclaredIn}` of `{s.Name}` must be a also defined as external symbol.");

                        var methodArgs = s.Args.Select(a => new E.MethodParameter(findType(a.Type), a.Name)).ToImmutableArray();
                        var returnType = findType(s.ResultType);
                        var newMethod = new E.MethodSignature(declaringType.Signature, methodArgs, s.Name, returnType, isStatic: s.Kind == ExternalSymbolKind.StaticMethod, E.Accessibility.APublic, false, false, false, false, ImmutableArray<E.GenericParameter>.Empty);
                        types[s.DeclaredIn] = declaringType.AddMember(E.MethodDef.CreateWithArray(newMethod, _ => E.Expression.Default(returnType)));
                        break;
                    }
                    default:
                        throw new NotSupportedException($"External symbols of kind {s.Kind} are not supported.");
                }
            }
            foreach (var t in types)
                cx.Metadata.AddType(t.Value, isExternal: true);
            // cx.Metadata.CommitWaitingTypes();
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
            var cx2 = E.MetadataContext.Create(settings.AdditionalReferences.Concat(E.MetadataContext.GetReferencedPaths()), GetEmitSettings(settings));
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
