using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.CSharpGen.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using TS=ICSharpCode.Decompiler.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using Xunit;
using Coberec.CSharpGen;

namespace Coberec.ExprCS
{
    /// <summary> Converts ExprCS metadata to ILSpy's typesystem classes. </summary>
    public static class MetadataDefiner
    {
        public static FullTypeName GetFullTypeName(this TypeSignature t) =>
            t.Parent.Match(
                ns => new FullTypeName(new TopLevelTypeName(ns.ToString(), t.Name, t.TypeParameters.Length)),
                parentType => GetFullTypeName(parentType).NestedType(t.Name, t.TypeParameters.Length)
            );

        public static TS.Accessibility GetAccessibility(Accessibility a) =>
            a == Accessibility.AInternal ? TS.Accessibility.Internal :
            a == Accessibility.APrivate ? TS.Accessibility.Private :
            a == Accessibility.APrivateProtected ? TS.Accessibility.ProtectedAndInternal :
            a == Accessibility.AProtected ? TS.Accessibility.Protected :
            a == Accessibility.AProtectedInternal ? TS.Accessibility.ProtectedOrInternal :
            a == Accessibility.APublic ? TS.Accessibility.Public :
            throw new NotImplementedException();

        // public static TS.ITypeDefinition GetTypeDefinition(this MetadataContext c, TypeSignature t) =>
        //     (TS.ITypeDefinition)c.DeclaredEntities.GetValueOrDefault(t) ??
        //     c.Compilation.FindType(t.GetFullTypeName()).GetDefinition() ??
        //     throw new Exception($"Could not resolve {t.GetFullTypeName()} for some reason.");


        public static TS.IType GetTypeReference(this MetadataContext c, TypeReference tref) =>
            tref.Match(
                specializedType =>
                    specializedType.GenericParameters.IsEmpty ? (IType)c.GetTypeDef(specializedType.Type) :
                    new ParameterizedType(c.GetTypeDef(specializedType.Type),
                        specializedType.GenericParameters.Select(p => GetTypeReference(c, p))),
                arrayType => new TS.ArrayType(c.Compilation, GetTypeReference(c, arrayType.Type), arrayType.Dimensions),
                byrefType => new TS.ByReferenceType(GetTypeReference(c, byrefType.Type)),
                pointerType => new TS.PointerType(GetTypeReference(c, pointerType.Type)),
                gParam => c.GenericParameterStore.Retreive(gParam),
                function => throw new NotSupportedException($"Function types are not supported in metadata")
            );

        public static IMethod GetMethod(this MetadataContext cx, MethodSignature method)
        {
            if (cx.DeclaredEntities.TryGetValue(method, out var declaredResult))
                return (IMethod)declaredResult;

            var t = cx.GetTypeDef(method.DeclaringType);

            var explicitInterface =
                method.Accessibility == Accessibility.APrivate && method.Name.Contains('.') ?
                t.GetAllBaseTypes().Where(b => b.Kind == TypeKind.Interface)
                                   .FirstOrDefault(i => method.Name.StartsWith(i.FullName + ".")) : null;

            var justName = explicitInterface is null ? method.Name
                                                     : method.Name.Substring(explicitInterface.FullName.Length + 1);

            bool filter(IMethod m) => m != null &&
                                      m.Name == method.Name &&
                                      m.Parameters.Count == method.Params.Length &&
                                      m.TypeParameters.Count == method.TypeParameters.Length &&
                                      SymbolLoader.TypeRef(m.ReturnType) == method.ResultType && // operators may be overloaded by result type
                                      m.Parameters.Select(p => SymbolLoader.TypeRef(p.Type)).SequenceEqual(method.Params.Select(a => a.Type));
            var candidates =
               (!method.HasSpecialName ? t.GetMethods(filter, GetMemberOptions.None) :
                method.Name == ".ctor" ? t.GetConstructors(filter, GetMemberOptions.None) :
                justName.StartsWith("get_") ? t.GetProperties(p => filter(p.Getter)).Select(p => p.Getter) :
                justName.StartsWith("set_") ? t.GetProperties(p => filter(p.Setter)).Select(p => p.Setter) :
                justName.StartsWith("remove_") ? t.GetEvents(e => filter(e.RemoveAccessor)).Select(p => p.RemoveAccessor) :
                justName.StartsWith("add_") ? t.GetEvents(e => filter(e.AddAccessor)).Select(p => p.AddAccessor) :
                justName.StartsWith("op_") ? t.GetMethods(filter, GetMemberOptions.None) :
                method.Name == "Finalize" ? t.GetMethods(filter, GetMemberOptions.None) :
                throw new NotSupportedException($"Special name {method.Name} is not supported"))
               .ToList();

            if (candidates.Count == 0)
                throw new Exception($"Method {method.Name} was not found on type {method.DeclaringType}. Method signature is {method}");

            var result = candidates.OrderByDescending(m => m.DeclaringType.GetAllBaseTypes().Count())
                                   .First();

            // make sure that there is only one such method
            Assert.Empty(candidates.Where(m => m != result && m.DeclaringType.GetAllBaseTypes().Count() == result.DeclaringType.GetAllBaseTypes().Count()));

            return result;
        }

        public static IMethod GetMethod(this MetadataContext cx, MethodReference method)
        {
            var m = GetMethod(cx, method.Signature);
            return m.Specialize(new TypeParameterSubstitution(
                method.TypeParameters.EagerSelect(t => GetTypeReference(cx, t)).NullIfEmpty(),
                method.MethodParameters.EagerSelect(t => GetTypeReference(cx, t)).NullIfEmpty()));
        }

        public static IField GetField(this MetadataContext cx, FieldSignature field)
        {
            if (cx.DeclaredEntities.TryGetValue(field, out var declaredResult))
                return (IField)declaredResult;

            var t = cx.GetTypeDef(field.DeclaringType);

            return t.GetFields(f => f.Name == field.Name, GetMemberOptions.IgnoreInheritedMembers).Single();
        }
        public static IField GetField(this MetadataContext cx, FieldReference field)
        {
            var f = GetField(cx, field.Signature);
            return (IField)f.Specialize(new TypeParameterSubstitution(field.TypeParameters.EagerSelect(t => GetTypeReference(cx, t)).NullIfEmpty(), null));
        }

        public static IProperty GetProperty(this MetadataContext cx, PropertySignature prop)
        {
            if (cx.DeclaredEntities.TryGetValue(prop, out var declaredResult))
                return (IProperty)declaredResult;

            var t = cx.GetTypeDef(prop.DeclaringType);

            return t.GetProperties(p => p.Name == prop.Name, GetMemberOptions.IgnoreInheritedMembers).Single();
        }

        public static IProperty GetProperty(this MetadataContext cx, PropertyReference prop)
        {
            var p = GetProperty(cx, prop.Signature);
            return (IProperty)p.Specialize(new TypeParameterSubstitution(prop.TypeParameters.EagerSelect(t => GetTypeReference(cx, t)).NullIfEmpty(), null));
        }

        public static IMember GetMember(this MetadataContext cx, MemberSignature sgn) =>
            sgn is MethodSignature method ? cx.GetMethod(method) :
            sgn is PropertySignature prop ? cx.GetProperty(prop) :
            sgn is FieldSignature field ? (IMember)cx.GetField(field) :
            throw new NotSupportedException();

        public static FullTypeName SanitizeTypeName(this MetadataContext cx, FullTypeName name, TypeDef type)
        {
            if (!cx.Settings.SanitizeSymbolNames)
                return name;

            Assert.False(name.IsNested);
            // {
            //     var declType = cx.Compilation.FindType(name.GetDeclaringType()).GetDefinition();
            //     var newName = SymbolNamer.NameMember(declType, name.Name, false);
            //     return name.GetDeclaringType().NestedType(name.Name, name.GetNestedTypeAdditionalTypeParameterCount(name.NestingLevel - 1));
            // }
            var t = name.TopLevelTypeName;
            var newName = SymbolNamer.NameType(type, cx.Compilation);
            return new FullTypeName(new TopLevelTypeName(t.Namespace, newName, t.TypeParameterCount));
        }

        public static VirtualType CreateTypeDefinition(MetadataContext cx, TypeDef t, FullTypeName? name = null)
        {
            if (t.Signature.Parent is TypeOrNamespace.TypeSignatureCase)
                Assert.NotNull(name);

            var sgn = t.Signature;
            var kind = sgn.Kind == "struct" ? TypeKind.Struct :
                       sgn.Kind == "interface" ? TypeKind.Interface :
                       sgn.Kind == "class" ? TypeKind.Class :
                       throw new NotSupportedException($"Type kind '{sgn.Kind}' is not supported.");

            var vt = new VirtualType(
                kind,
                GetAccessibility(sgn.Accessibility),
                name ?? cx.SanitizeTypeName(sgn.GetFullTypeName(), t),
                isStatic: !sgn.CanOverride && sgn.IsAbstract,
                isSealed: !sgn.CanOverride,
                sgn.IsAbstract,
                sgn.Parent is TypeOrNamespace.TypeSignatureCase tt ? cx.GetTypeDef(tt.Item) : null,
                parentModule: cx.MainILSpyModule
            );

            Assert.Equal((bool)vt.IsReferenceType, !sgn.IsValueType);

            cx.RegisterEntity(t, vt);
            return vt;
        }

        internal static IParameter CreateParameter(MetadataContext cx, MethodParameter p) =>
            new DefaultParameter(
                GetTypeReference(cx, p.Type),
                p.Name,
                referenceKind: p.Type is TypeReference.ByReferenceTypeCase ? ReferenceKind.Ref : ReferenceKind.None,
                isOptional: p.HasDefaultValue,
                defaultValue: p.HasDefaultValue ? p.DefaultValue : null
            );

        internal static VirtualMethod CreateMethodDefinition(MetadataContext cx, MethodDef m, string name, bool isHidden = false, bool sneaky = false)
        {
            var sgn = m.Signature;
            var declType = cx.GetTypeDef(sgn.DeclaringType);
            IParameter[] parameters() => SymbolNamer.NameParameters(sgn.Params.Select(p => CreateParameter(cx, p)));

            foreach (var i in m.Implements)
                if (i.Signature.DeclaringType.Kind == "interface")
                    Assert.Contains(i.Signature.DeclaringType, cx.GetDirectImplements(sgn.DeclaringType.SpecializeByItself()).Select(t => t.Type));
                else
                    Assert.Contains(i.Signature.DeclaringType, cx.GetBaseTypes(sgn.DeclaringType.SpecializeByItself()).Select(t => t.Type));

            return new VirtualMethod(
                declType,
                GetAccessibility(sgn.Accessibility),
                name,
                _ => parameters(),
                _ => GetTypeReference(cx, sgn.ResultType),
                sgn.IsOverride,
                isVirtual: sgn.IsVirtual && !sgn.IsOverride,
                isSealed: sgn.IsOverride && !sgn.IsVirtual && sgn.DeclaringType.CanOverride,
                sgn.IsAbstract,
                sgn.IsStatic,
                isHidden,
                typeParameters: sgn.TypeParameters.Select<GenericParameter, Func<IEntity, int, ITypeParameter>>(p => (owner, index) => cx.GenericParameterStore.Register(p, owner, index)).ToArray()
            )
            .ApplyAction(mm => { if (!sneaky) cx.RegisterEntity(m, mm); });
        }

        internal static void AddExplicitImplementations(VirtualType type, MetadataContext cx, MethodDef method, string name)
        {
            var implements = method.Implements
                .Where(i => i.Signature.DeclaringType.Kind == "interface")
                .Select(cx.GetMethod)
                .Where(m => m.Name != name || method.Signature.Accessibility != Accessibility.APublic || SymbolLoader.TypeRef(m.ReturnType) != method.Signature.ResultType)
                .ToArray();
            foreach (var i in implements)
            {
                var newReturnType = SymbolLoader.TypeRef(i.ReturnType);
                var m2 = new VirtualMethod(type, TS.Accessibility.Private, i.DeclaringType.FullName + "." + i.Name, _ => i.Parameters, _ => i.ReturnType, typeParameters: i.TypeParameters.Select<ITypeParameter, Func<IEntity, int, ITypeParameter>>(p => (owner, index) => new TS.Implementation.DefaultTypeParameter(owner, index, name: p.Name)).ToArray(), explicitImplementations: new IMember[] { i });
                var m2_def = MethodDef.CreateWithArray(
                    method.Signature.Clone().With(resultType: newReturnType),
                    args => ((Expression)args[0]).CallMethod(method.Signature, args.Skip(1).Select(Expression.Parameter)).ReferenceConvert(newReturnType)
                );
                m2.BodyFactory = CreateBodyFactory(m2, m2_def, cx);
                type.Methods.Add(m2);
            }
        }

        internal static void AddExplicitImplementations(VirtualType type, MetadataContext cx, PropertyDef property, string name)
        {
            var implements = property.Implements
                .Where(i => i.Signature.DeclaringType.Kind == "interface")
                .Select(cx.GetProperty)
                .Where(p => p.Name != name || property.Signature.Accessibility != Accessibility.APublic || SymbolLoader.TypeRef(p.ReturnType) != property.Signature.Type)
                .ToArray();
            foreach (var i in implements)
            {
                var newReturnType = SymbolLoader.TypeRef(i.ReturnType);
                var getter_s = property.Getter?.Signature.With(accessibility: Accessibility.APrivate, resultType: newReturnType);
                var getter = property.Getter?.Apply(m => CreateMethodDefinition(cx, m.With(signature: getter_s), i.DeclaringType.FullName + "." + "get_" + name, isHidden: true, sneaky: true));
                var setter_s = property.Setter?.Signature.With(accessibility: Accessibility.APrivate);
                var setter = property.Setter?.Apply(m => CreateMethodDefinition(cx, m.With(signature: setter_s), i.DeclaringType.FullName + "." + "set_" + name, isHidden: true, sneaky: true));
                var p2 = new VirtualProperty(type, TS.Accessibility.Private, i.DeclaringType.FullName + "." + i.Name, getter, setter, explicitImplementations: new IMember[] { i });

                if (getter is object)
                {
                    type.Methods.Add(getter);
                    var g_def = MethodDef.CreateWithArray(getter_s, args => ((Expression)args[0]).CallMethod(property.Getter.Signature, args.Skip(1).Select(Expression.Parameter)).ReferenceConvert(newReturnType));
                    getter.BodyFactory = CreateBodyFactory(getter, g_def, cx);
                }
                if (setter is object)
                {
                    type.Methods.Add(setter);
                    var g_def = MethodDef.CreateWithArray(setter_s, args => ((Expression)args[0]).CallMethod(property.Setter.Signature, args.Skip(1).Select(Expression.Parameter)));
                    setter.BodyFactory = CreateBodyFactory(setter, g_def, cx);
                }
                type.Properties.Add(p2);
            }
        }

        internal static (VirtualProperty, VirtualMethod, VirtualMethod) CreatePropertyDefinition(MetadataContext cx, PropertyDef property, string name)
        {
            Assert.Equal(property.Signature.Getter == null, property.Getter == null);
            Assert.Equal(property.Signature.Setter == null, property.Setter == null);

            var getter = property.Getter?.Apply(m => CreateMethodDefinition(cx, m, "get_" + name, isHidden: true));
            var setter = property.Setter?.Apply(m => CreateMethodDefinition(cx, m, "set_" + name, isHidden: true));
            if (property.Getter?.Body != null) getter.Attributes.Add(cx.Compilation.CompilerGeneratedAttribute()); // TODO: this is not very nice :/
            if (property.Setter?.Body != null) setter.Attributes.Add(cx.Compilation.CompilerGeneratedAttribute());

            var sgn = property.Signature;
            var declType = cx.GetTypeDef(sgn.DeclaringType);

            foreach (var i in property.Implements)
                if (i.Signature.DeclaringType.Kind == "interface")
                    Assert.Contains(i.Signature.DeclaringType, cx.GetDirectImplements(sgn.DeclaringType.SpecializeByItself()).Select(t => t.Type));
                else
                    Assert.Contains(i.Signature.DeclaringType, cx.GetBaseTypes(sgn.DeclaringType.SpecializeByItself()).Select(t => t.Type));

            var mSgn = sgn.Getter ?? sgn.Setter;

            var prop = new VirtualProperty(
                declType,
                GetAccessibility(sgn.Accessibility),
                name,
                getter,
                setter,
                isIndexer: false, // TODO: indexers?
                mSgn.IsVirtual,
                mSgn.IsOverride,
                sgn.IsStatic,
                mSgn.IsAbstract,
                !mSgn.IsVirtual && mSgn.IsOverride
            );
            cx.RegisterEntity(property, prop);
            return (prop, getter, setter);
        }

        internal static VirtualField CreateFieldDefinition(MetadataContext cx, FieldDef field, string name)
        {
            var sgn = field.Signature;
            var declType = cx.GetTypeDef(sgn.DeclaringType);
            var special = SymbolNamer.IsSpecial(sgn);
            var result = new VirtualField(
                declType,
                GetAccessibility(sgn.Accessibility),
                name,
                GetTypeReference(cx, sgn.ResultType),
                isReadOnly: sgn.IsReadonly,
                isVolatile: false,
                isStatic: sgn.IsStatic,
                isHidden: special
            );
            if (special)
                result.Attributes.Add(cx.Compilation.CompilerGeneratedAttribute());

            cx.RegisterEntity(field, result);
            return result;
        }

        static Func<IL.ILFunction> CreateBodyFactory(VirtualMethod resultMethod, MethodDef method, MetadataContext cx)
        {
            if (resultMethod.DeclaringType.Kind == TypeKind.Interface || resultMethod.IsAbstract)
            {
                if (method.Body != null) throw new NotSupportedException($"Interface and abstract method can not have a body (Default interface implementation are not supported). Method {method.Signature} does.");
                return null;
            }
            if (method.Body is null)
                throw new Exception($"Method {method.Signature} was expected to have body, but there is null in the MethodDef.");

            return () => CodeTranslation.CodeTranslator.CreateBody(method, resultMethod, cx);
        }

        public static void DefineTypeMembers(VirtualType type, MetadataContext cx, TypeDef definition, bool isHidden)
        {
            if (isHidden) type.IsHidden = true;

            definition = ImplementationResolver.AutoResolveImplementations(definition, cx);

            if (definition.Extends is object)
            {
                type.DirectBaseType = GetTypeReference(cx, definition.Extends);
            }
            foreach (var implements in definition.Implements)
            {
                type.ImplementedInterfaces.Add(GetTypeReference(cx, implements));
            }

            foreach (var a in cx.GetTypeMods(definition.Signature))
                a.DeclareMembers(type);

            var names = cx.Settings.SanitizeSymbolNames ? SymbolNamer.NameMembers(definition, type, cx.Settings.AdjustCasing) :
                        definition.Members.ToDictionary(m => m.Signature, m => m.Signature.Name);

            // declare the types first, as they may be used in the type
            foreach (var typeMember in definition.Members.OfType<TypeDef>())
            {
                var name = names[typeMember.Signature];
                var d = CreateTypeDefinition(cx, typeMember, type.FullTypeName.NestedType(name, typeMember.Signature.TypeParameters.Length));

                if (isHidden) d.IsHidden = true;
                type.NestedTypes.Add(d);
            }

            foreach (var member in definition.Members)
            {
                var name = names[member.Signature];
                if (member is MethodDef method)
                {
                    Assert.Equal(definition.Signature, method.Signature.DeclaringType);
                    var d = CreateMethodDefinition(cx, method, name);
                    type.Methods.Add(d);
                    d.BodyFactory = isHidden ? null : CreateBodyFactory(d, method, cx);

                    AddExplicitImplementations(type, cx, method, name);
                }
                else if (member is TypeDef typeMember)
                {
                    var d = (VirtualType)type.NestedTypes.Single(t => t.Name == name);
                    DefineTypeMembers(d, cx, typeMember, isHidden);
                }
                else if (member is FieldDef field)
                {
                    Assert.Equal(definition.Signature, field.Signature.DeclaringType);
                    var d = CreateFieldDefinition(cx, field, name);
                    type.Fields.Add(d);
                }
                else if (member is PropertyDef prop)
                {
                    Assert.Equal(definition.Signature, prop.Signature.DeclaringType);
                    var (p, getter, setter) = CreatePropertyDefinition(cx, prop, name);
                    getter?.ApplyAction(type.Methods.Add);
                    setter?.ApplyAction(type.Methods.Add);
                    type.Properties.Add(p);
                    if (getter != null)
                        getter.BodyFactory = isHidden ? null : CreateBodyFactory(getter, prop.Getter, cx);
                    if (setter != null)
                        setter.BodyFactory = isHidden ? null : CreateBodyFactory(setter, prop.Setter, cx);

                    AddExplicitImplementations(type, cx, prop, name);
                }
                else throw new NotImplementedException($"Member '{member}' of type '{member.GetType().Name}'");
            }

            foreach (var a in cx.GetTypeMods(definition.Signature))
                a.CompleteDefinitions?.Invoke(type);
        }

        /// Orders the definitions so that base types and implemented interfaces are declared before use
        public static TypeDef[] SortDefinitions(IList<TypeDef> types)
        {
            var lookup = types.ToDictionary(t => t.Signature);
            var result = new List<TypeDef>();

            void add(TypeSignature t)
            {
                if (lookup.TryGetValue(t, out var td))
                {
                    lookup.Remove(t);
                    result.Add(td);
                    foreach (var tt in td.Implements) add(tt.Type);
                    if (td.Extends is object) add(td.Extends.Type);
                }
            }

            foreach (var t in types) add(t.Signature);

            Assert.Equal(result.Count, types.Count);
            return result.ToArray();
        }

    }
}
