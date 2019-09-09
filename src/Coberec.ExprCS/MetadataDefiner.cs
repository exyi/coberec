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
    internal static class MetadataDefiner
    {
        public static FullTypeName GetFullTypeName(this TypeSignature t) =>
            t.Parent.Match(
                ns => new FullTypeName(new TopLevelTypeName(ns.Item.ToString(), t.Name, t.GenericParamCount)),
                parentType => GetFullTypeName(parentType.Item).NestedType(t.Name, t.GenericParamCount)
            );

        public static TS.Accessibility GetAccessibility(Accessibility a) =>
            a == Accessibility.AInternal ? TS.Accessibility.Internal :
            a == Accessibility.APrivate ? TS.Accessibility.Private :
            a == Accessibility.APrivateProtected ? TS.Accessibility.ProtectedAndInternal :
            a == Accessibility.AProtected ? TS.Accessibility.Protected :
            a == Accessibility.AProtectedInternal ? TS.Accessibility.ProtectedOrInternal :
            a == Accessibility.APublic ? TS.Accessibility.Public :
            throw new NotImplementedException();

        public static TS.ITypeDefinition GetTypeDefinition(this MetadataContext c, TypeSignature t) =>
            c.Compilation.FindType(t.GetFullTypeName()).GetDefinition() ?? throw new Exception($"Could not resolve {t.GetFullTypeName()} for some reason.");

        public static TS.IType GetTypeReference(this MetadataContext c, TypeReference tref) =>
            tref.Match(
                specializedType =>
                    specializedType.Item.GenericParameters.IsEmpty ? (IType)GetTypeDefinition(c, specializedType.Item.Type) :
                    new ParameterizedType(GetTypeDefinition(c, specializedType.Item.Type),
                        specializedType.Item.GenericParameters.Select(p => GetTypeReference(c, p))),
                arrayType => new TS.ArrayType(c.Compilation, GetTypeReference(c, arrayType.Item.Type), arrayType.Item.Dimensions),
                byrefType => new TS.ByReferenceType(GetTypeReference(c, byrefType.Item.Type)),
                pointerType => new TS.PointerType(GetTypeReference(c, pointerType.Item.Type)),
                gParam => throw new NotSupportedException(),
                function => throw new NotSupportedException($"Function types are not supported in metadata")
            );

        public static IMethod GetMethod(this MetadataContext cx, MethodSignature method)
        {
            var t = cx.GetTypeReference(method.DeclaringType); // TODO: generic methods

            bool filter(IMethod m) => m.Name == method.Name &&
                                      m.Parameters.Count == method.Params.Length &&
                                      m.Parameters.Select(p => cx.TranslateTypeReference(p.Type)).SequenceEqual(method.Params.Select(a => a.Type));

            var candidates =
               (!method.HasSpecialName ? t.GetMethods(filter, GetMemberOptions.None) :
                method.Name == ".ctor" ? t.GetConstructors(filter, GetMemberOptions.None) :
                method.Name.StartsWith("get_") ? t.GetProperties(p => filter(p.Getter)).Select(p => p.Getter) :
                method.Name.StartsWith("set_") ? t.GetProperties(p => filter(p.Setter)).Select(p => p.Setter) :
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

        public static IField GetField(this MetadataContext cx, FieldSignature field)
        {
            var t = cx.GetTypeReference(field.DeclaringType); // TODO: generic methods

            return t.GetFields(f => f.Name == field.Name, GetMemberOptions.None).Single();
        }

        public static FullTypeName SanitizeName(this MetadataContext cx, FullTypeName name)
        {
            if (!cx.Settings.SanitizeSymbolNames)
                return name;

            if (name.IsNested)
            {
                var declType = cx.Compilation.FindType(name.GetDeclaringType()).GetDefinition();
                var newName = SymbolNamer.NameMember(declType, name.Name, false);
                return name.GetDeclaringType().NestedType(name.Name, name.GetNestedTypeAdditionalTypeParameterCount(name.NestingLevel - 1));
            }
            else
            {
                var t = name.TopLevelTypeName;
                var newName = SymbolNamer.NameType(t.Namespace, t.Name, t.TypeParameterCount, cx.Compilation);
                return new FullTypeName(new TopLevelTypeName(t.Namespace, newName, t.TypeParameterCount));
            }
        }

        public static VirtualType CreateTypeDefinition(MetadataContext c, TypeDef t)
        {
            var sgn = t.Signature;
            var kind = sgn.Kind == "struct" ? TypeKind.Struct :
                       sgn.Kind == "interface" ? TypeKind.Interface :
                       sgn.Kind == "class" ? TypeKind.Class :
                       throw new NotSupportedException($"Type kind '{sgn.Kind}' is not supported.");

            var vt = new VirtualType(
                kind,
                GetAccessibility(sgn.Accessibility),
                c.SanitizeName(sgn.GetFullTypeName()),
                isStatic: !sgn.CanOverride && sgn.IsAbstract,
                isSealed: !sgn.CanOverride,
                sgn.IsAbstract,
                sgn.Parent is TypeOrNamespace.TypeSignatureCase tt ? c.GetTypeDef(tt.Item) : null,
                parentModule: c.MainILSpyModule
            );

            Assert.Equal((bool)vt.IsReferenceType, !sgn.IsValueType);

            return vt;
        }

        internal static IParameter CreateParameter(MetadataContext cx, MethodParameter p) =>
            new DefaultParameter(GetTypeReference(cx, p.Type), p.Name, referenceKind: p.Type is TypeReference.ByReferenceTypeCase ? ReferenceKind.Ref : ReferenceKind.None);

        internal static VirtualMethod CreateMethodDefinition(MetadataContext cx, MethodDef m, bool isHidden = false)
        {
            var sgn = m.Signature;
            var declType = cx.GetTypeDef(sgn.DeclaringType);
            var parameters = sgn.Params.Select(p => CreateParameter(cx, p)).ToArray();
            var name =
                cx.Settings.SanitizeSymbolNames && !sgn.HasSpecialName
                    ? SymbolNamer.NameMethod(declType, sgn.Name, sgn.TypeParameters.Length, parameters, sgn.IsOverride)
                    : sgn.Name;

            return new VirtualMethod(
                declType,
                GetAccessibility(sgn.Accessibility),
                name,
                parameters,
                GetTypeReference(cx, sgn.ResultType),
                sgn.IsOverride,
                isVirtual: sgn.IsVirtual && !sgn.IsOverride,
                isSealed: sgn.IsOverride && !sgn.IsVirtual,
                sgn.IsAbstract,
                sgn.IsStatic,
                isHidden,
                sgn.TypeParameters.Select<GenericParameter, ITypeParameter>(a => throw new NotImplementedException()).ToArray()
                // TODO: explicitImplementations
            );
        }

        static (VirtualProperty, VirtualMethod, VirtualMethod) CreatePropertyDefinition(MetadataContext cx, PropertyDef property)
        {
            Assert.Equal(property.Signature.Getter == null, property.Getter == null);
            Assert.Equal(property.Signature.Setter == null, property.Setter == null);

            var getter = property.Getter?.Apply(m => CreateMethodDefinition(cx, m, isHidden: true));
            var setter = property.Setter?.Apply(m => CreateMethodDefinition(cx, m, isHidden: true));

            var sgn = property.Signature;
            var declType = cx.GetTypeDef(sgn.DeclaringType);
            var name =
                cx.Settings.SanitizeSymbolNames ? SymbolNamer.NameMember(declType, sgn.Name, null)
                                                : sgn.Name;

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
                // TODO: explicit implementations
            );
            return (prop, getter, setter);
        }

        static VirtualField CreateFieldDefinition(MetadataContext cx, FieldDef field)
        {
            var sgn = field.Signature;
            var declType = cx.GetTypeDef(sgn.DeclaringType);
            var name =
                cx.Settings.SanitizeSymbolNames ? SymbolNamer.NameMember(declType, sgn.Name, null)
                                                : sgn.Name;
            return new VirtualField(
                declType,
                GetAccessibility(sgn.Accessibility),
                name,
                GetTypeReference(cx, sgn.ResultType),
                isReadOnly: sgn.IsReadonly,
                isVolatile: false,
                isStatic: sgn.IsStatic
            );
        }

        static Func<IL.ILFunction> CreateBodyFactory(VirtualMethod resultMethod, MethodDef method, MetadataContext cx)
        {
            if (resultMethod.DeclaringType.Kind == TypeKind.Interface)
            {
                if (method.Body != null) throw new NotSupportedException($"Default interface implementation are not supported.");
                return null;
            }
            Assert.NotNull(method.Body);

            return () => CodeTranslation.CodeTranslator.CreateBody(method, resultMethod, cx);
        }

        public static void DefineTypeMembers(VirtualType type, MetadataContext cx, TypeDef definition)
        {
            if (definition.Extends is object)
            {
                type.DirectBaseType = GetTypeReference(cx, definition.Extends);
            }
            foreach (var implements in definition.Implements)
            {
                type.ImplementedInterfaces.Add(GetTypeReference(cx, implements));
            }
            foreach (var member in definition.Members)
            {
                if (member is MethodDef method)
                {
                    Assert.Equal(definition.Signature, method.Signature.DeclaringType);
                    var d = CreateMethodDefinition(cx, method);
                    type.Methods.Add(d);
                    d.BodyFactory = CreateBodyFactory(d, method, cx);
                }
                else if (member is TypeDef typeMember)
                {
                    // Assert.Equal(definition.Signature, typeMember.Signature.Parent);
                    var d = CreateTypeDefinition(cx, typeMember);
                    type.NestedTypes.Add(d);
                    DefineTypeMembers(d, cx, typeMember);
                }
                else if (member is FieldDef field)
                {
                    Assert.Equal(definition.Signature, field.Signature.DeclaringType);
                    var d = CreateFieldDefinition(cx, field);
                    type.Fields.Add(d);
                }
                else if (member is PropertyDef prop)
                {
                    Assert.Equal(definition.Signature, prop.Signature.DeclaringType);
                    var (p, getter, setter) = CreatePropertyDefinition(cx, prop);
                    getter?.ApplyAction(type.Methods.Add);
                    setter?.ApplyAction(type.Methods.Add);
                    type.Properties.Add(p);
                    if (getter != null)
                        getter.BodyFactory = CreateBodyFactory(getter, prop.Getter, cx);
                    if (setter != null)
                        setter.BodyFactory = CreateBodyFactory(setter, prop.Setter, cx);
                }
                else throw new NotImplementedException($"Member '{member}' of type '{member.GetType().Name}'");
            }
        }

    }
}
