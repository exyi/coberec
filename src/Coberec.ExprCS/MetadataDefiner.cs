using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.CSharpGen.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using TS=ICSharpCode.Decompiler.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using Xunit;

namespace Coberec.ExprCS
{
    internal static class MetadataDefiner
    {
        public static FullTypeName GetFullTypeName(this TypeSignature t) =>
            t.Parent.Match(
                ns => new FullTypeName(new TopLevelTypeName(ns.Item.FullName(), t.Name, t.GenericParamCount)),
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
            c.Compilation.FindType(t.GetFullTypeName()).GetDefinition();

        public static TS.IType GetTypeReference(this MetadataContext c, TypeReference tref) =>
            tref.Match(
                specializedType =>
                    specializedType.Item.GenericParameters.IsEmpty ? (IType)GetTypeDefinition(c, specializedType.Item.Type) :
                    new ParameterizedType(GetTypeDefinition(c, specializedType.Item.Type),
                        specializedType.Item.GenericParameters.Select(p => GetTypeReference(c, p))),
                arrayType => new TS.ArrayType(c.Compilation, GetTypeReference(c, arrayType.Item.Type), arrayType.Item.Dimensions),
                byrefType => new TS.ByReferenceType(GetTypeReference(c, byrefType)),
                pointerType => new TS.PointerType(GetTypeReference(c, pointerType.Item.Type)),
                gParam => throw new NotSupportedException(),
                function => throw new NotSupportedException($"Function types are not supported in metadata")
            );

        public static IMethod GetMethod(this MetadataContext cx, MethodSignature method)
        {
            var t = cx.GetTypeReference(method.DeclaringType); // TODO: generic methods
            // TODO: constructors, accessors and stuff
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

        public static VirtualType CreateTypeDefinition(MetadataContext c, TypeDef t)
        {
            var sgn = t.Signature;
            var vt = new VirtualType(
                TypeKind.Class, // TODO
                GetAccessibility(sgn.Accessibility),
                sgn.GetFullTypeName(),
                isStatic: false, // TODO
                sgn.IsSealed,
                sgn.IsAbstract,
                sgn.Parent is TypeOrNamespace.TypeSignatureCase tt ? c.GetTypeDef(tt.Item) : null,
                parentModule: c.MainILSpyModule
            );

            return vt;
        }

        internal static IParameter CreateParameter(MetadataContext cx, MethodParameter p) =>
            new DefaultParameter(GetTypeReference(cx, p.Type), p.Name);

        internal static VirtualMethod CreateMethodDefinition(MetadataContext cx, MethodDef m)
        {
            var sgn = m.Signature;
            return new VirtualMethod(
                cx.GetTypeDef(sgn.DeclaringType),
                GetAccessibility(sgn.Accessibility),
                sgn.Name,
                sgn.Params.Select(p => CreateParameter(cx, p)).ToArray(),
                GetTypeReference(cx, sgn.ResultType),
                sgn.IsOverride,
                sgn.IsVirtual,
                isSealed: sgn.IsOverride && !sgn.IsVirtual,
                sgn.IsAbstract,
                sgn.IsStatic,
                isHidden: false,
                sgn.TypeParameters.Select<GenericParameter, ITypeParameter>(a => throw new NotImplementedException()).ToArray()
                // TODO: explicitImplementations
            );
        }
        static VirtualField CreateFieldDefinition(MetadataContext c, FieldDef field)
        {
            var sgn = field.Signature;
            return new VirtualField(
                c.GetTypeDef(sgn.DeclaringType),
                GetAccessibility(sgn.Accessibility),
                sgn.Name,
                GetTypeReference(c, sgn.ResultType),
                isReadOnly: sgn.IsReadonly,
                isVolatile: false,
                isStatic: sgn.IsStatic
            );
        }

        static Func<IL.ILFunction> CreateBodyFactory(VirtualMethod resultMethod, MethodDef method, MetadataContext cx)
        {
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
                    var d = CreateMethodDefinition(cx, method);
                    type.Methods.Add(d);
                    d.BodyFactory = CreateBodyFactory(d, method, cx);
                }
                else if (member is TypeDef typeMember)
                {
                    var d = CreateTypeDefinition(cx, typeMember);
                    type.NestedTypes.Add(d);
                    DefineTypeMembers(d, cx, typeMember);
                }
                else if (member is FieldDef field)
                {
                    var d = CreateFieldDefinition(cx, field);
                    type.Fields.Add(d);
                }
                else throw new NotImplementedException($"Member '{member}' of type '{member.GetType().Name}'");
            }
        }

    }
}
