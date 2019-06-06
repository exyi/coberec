using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.CSharpGen.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using TS=ICSharpCode.Decompiler.TypeSystem;

namespace Coberec.ExprCS
{
    static class MetadataDefiner
    {
        public static FullTypeName GetFullTypeName(this TypeSignature t) =>
            t.Parent.Match(
                ns => new FullTypeName(new TopLevelTypeName(ns.Item.FullName(), t.Name, t.GenericArgCount)),
                parentType => GetFullTypeName(parentType.Item).NestedType(t.Name, t.GenericArgCount)
            );

        public static TS.Accessibility GetAccessibility(Accessibility a) =>
            a == Accessibility.AInternal ? TS.Accessibility.Internal :
            a == Accessibility.APrivate ? TS.Accessibility.Private :
            a == Accessibility.APrivateProtected ? TS.Accessibility.ProtectedAndInternal :
            a == Accessibility.AProtected ? TS.Accessibility.Protected :
            a == Accessibility.AProtectedInternal ? TS.Accessibility.ProtectedOrInternal :
            a == Accessibility.APublic ? TS.Accessibility.Public :
            throw new NotImplementedException();

        public static TS.ITypeDefinition GetTypeDefinition(MetadataContext c, TypeSignature t) =>
            c.Compilation.FindType(t.GetFullTypeName()).GetDefinition();

        public static TS.IType GetTypeReference(MetadataContext c, TypeReference tref) =>
            tref.Match(
                specializedType =>
                    specializedType.Item.GenericParameters.IsEmpty ? (IType)GetTypeDefinition(c, specializedType.Item.Type) :
                    new ParameterizedType(GetTypeDefinition(c, specializedType.Item.Type),
                        specializedType.Item.GenericParameters.Select(p => GetTypeReference(c, p))),
                arrayType => new TS.ArrayType(c.Compilation, GetTypeReference(c, arrayType.Item.Type), arrayType.Item.Dimensions),
                byrefType => new TS.ByReferenceType(GetTypeReference(c, byrefType)),
                pointerType => new TS.PointerType(GetTypeReference(c, pointerType.Item.Type)),
                gParam => throw new NotSupportedException()
            );

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

        static VirtualMethod CreateMethodDefinition(MetadataContext c, MethodDef m)
        {
            var sgn = m.Signature;
            return new VirtualMethod(
                c.GetTypeDef(sgn.DeclaringType),
                GetAccessibility(sgn.Accessibility),
                sgn.Name,
                sgn.Args.Select(a => new DefaultParameter(GetTypeReference(c, a.Type), a.Name)).ToArray(),
                GetTypeReference(c, sgn.ResultType),
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

        public static void DefineTypeMembers(VirtualType type, MetadataContext c, TypeDef definition)
        {
            if (definition.Extends is object)
            {
                type.DirectBaseType = GetTypeReference(c, definition.Extends);
            }
            foreach (var implements in definition.Implements)
            {
                type.ImplementedInterfaces.Add(GetTypeReference(c, implements));
            }
            foreach (var member in definition.Members)
            {
                if (member is MethodDef method)
                {
                    var d = CreateMethodDefinition(c, method);
                    type.Methods.Add(d);
                }
                else if (member is TypeDef typeMember)
                {
                    var d = CreateTypeDefinition(c, typeMember);
                    type.NestedTypes.Add(d);
                    DefineTypeMembers(d, c, typeMember);
                }
                else if (member is FieldDef field)
                {
                    var d = CreateFieldDefinition(c, field);
                    type.Fields.Add(d);
                }
                else throw new NotImplementedException($"Member '{member}' of type '{member.GetType().Name}'");
            }
        }

    }
}
