using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests
{
    public class MetadataDefinitionTests
    {
        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create("MyModule");
        [Fact]
        public void OneEmptyType()
        {
            var ns = NamespaceSignature.Parse("MyNamespace");
            var type = TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var typeDef = TypeDef.Empty(type);

            cx.AddType(typeDef);
            check.CheckOutput(cx);
        }

        [Fact]
        public void NestedTypesWithInheritance()
        {
            var ns = NamespaceSignature.Parse("MyNamespace");
            var rootType = TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var type1 = TypeSignature.Class("A", rootType, Accessibility.APublic);
            var type2 = type1.With(name: "B");
            var typeDef = TypeDef.Empty(rootType).With(members: ImmutableArray.Create<MemberDef>(
                new TypeDef(type1, null, ImmutableArray<SpecializedType>.Empty, ImmutableArray<MemberDef>.Empty),
                new TypeDef(type2, new SpecializedType(type1, ImmutableArray<TypeReference>.Empty), ImmutableArray<SpecializedType>.Empty, ImmutableArray<MemberDef>.Empty)
            ));

            cx.AddType(typeDef);


            var rootType2 = typeDef.Signature.With(name: "MyType2");
            cx.AddType(typeDef.With(
                signature: rootType2,
                extends: new SpecializedType(type2, ImmutableArray<TypeReference>.Empty),
                members: typeDef.Members.OfType<TypeDef>().Select(m => m.With(signature: m.Signature.With(parent: TypeOrNamespace.TypeSignature(rootType2)))).ToImmutableArray<MemberDef>()
            ));

            check.CheckOutput(cx);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IEquatableImplementation(bool isStruct)
        {
            var ns = NamespaceSignature.Parse("MyNamespace");
            var type = isStruct ? TypeSignature.Struct("MyType", ns, Accessibility.APublic)
                                : TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var iequatableT = TypeSignature.FromType(typeof(IEquatable<>));
            var method = new MethodSignature(type, ImmutableArray.Create(new MethodParameter(type, "obj")), "Equals", TypeReference.FromType(typeof(bool)), false, Accessibility.APublic, false, false, false, false, ImmutableArray<GenericParameter>.Empty);
            var methodDef = MethodDef.Create(method, (_thisP, _objP) => new ConstantExpression(true, TypeReference.FromType(typeof(bool))));
            var typeDef = TypeDef.Empty(type).With(
                implements: ImmutableArray.Create(new SpecializedType(iequatableT, ImmutableArray.Create<TypeReference>(type))),
                members: ImmutableArray.Create<MemberDef>(methodDef));

            cx.AddType(typeDef);


            check.CheckOutput(cx, $"{(isStruct ? "struct" : "class")}");
        }

        [Fact]
        public void FewFields()
        {
            var stringT = TypeReference.FromType(typeof(string));
            var stringArr = TypeReference.FromType(typeof(string[]));
            var someTuple = TypeReference.FromType(typeof((List<int>, System.Threading.Tasks.Task)));

            var ns = NamespaceSignature.Parse("MyNamespace");
            var type = TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var typeDef = TypeDef.Empty(type).With(members: ImmutableArray.Create<MemberDef>(
                new FieldDef(new FieldSignature(type, "F1", Accessibility.APublic, stringT, false, true)),
                new FieldDef(new FieldSignature(type, "F2", Accessibility.APrivate, stringArr, false, true)),
                new FieldDef(new FieldSignature(type, "F3", Accessibility.AInternal, someTuple, false, true)),
                new FieldDef(new FieldSignature(type, "F4", Accessibility.AProtectedInternal, stringArr, true, false))
            ));

            cx.AddType(typeDef);
            check.CheckOutput(cx);
        }

        [Fact]
        public void Interface()
        {
            var ns = NamespaceSignature.Parse("MyNamespace");
            var type = TypeSignature.Interface("MyInterface", ns, Accessibility.APublic);

            var method = MethodSignature.Instance("MyMethod", type, Accessibility.APublic, TypeSignature.Int32, new MethodParameter(TypeSignature.String, "myParameter"));
            var property = PropertySignature.Create("MyProperty", type, TypeSignature.Boolean, Accessibility.APublic, null);
            var typeDef = TypeDef.Empty(type)
                .AddMember(MethodDef.InterfaceDef(method))
                .AddMember(PropertyDef.InterfaceDef(property));


            cx.AddType(typeDef);
            check.CheckOutput(cx);
        }

        [Fact]
        public void ParameterDefaultValues()
        {
            var ns = NamespaceSignature.Parse("MyNamespace");
            var type = TypeSignature.Interface("MyInterface2", ns, Accessibility.APublic);

            var method1 = MethodSignature.Instance("StringMethod", type, Accessibility.APublic, TypeSignature.Int32, new MethodParameter(TypeSignature.String, "myParameter", hasDefaultValue: true, "default value"));
            var method2 = MethodSignature.Instance("ValueTypeMethod", type, Accessibility.APublic, TypeSignature.Int32, new MethodParameter(TypeSignature.FromType(typeof(Guid)), "myParameter", hasDefaultValue: true, null));
            var typeDef = TypeDef.Empty(type)
                .AddMember(MethodDef.InterfaceDef(method1))
                .AddMember(MethodDef.InterfaceDef(method2));


            cx.AddType(typeDef);
            check.CheckOutput(cx);
        }

        [Fact]
        public void NameSanitization()
        {
            var stringT = TypeReference.FromType(typeof(string));

            var type = TypeSignature.Class("MyType", NamespaceSignature.Parse("MyNamespace"), Accessibility.APublic);
            cx.AddType(TypeDef.Empty(type).AddMember(
                // Should be renamed, there is collision with virtual object.Equals
                new FieldDef(new FieldSignature(type, "Equals", Accessibility.APublic, stringT, false, true))
            ));
            var type2 = TypeSignature.Class("MyType2", NamespaceSignature.Parse("MyNamespace"), Accessibility.APublic);
            cx.AddType(TypeDef.Empty(type2).AddMember(
                // OK, no collision here
                new MethodDef(
                    MethodSignature.Instance("Equals", type2, Accessibility.APublic, TypeSignature.Boolean),
                    ImmutableArray.Create(ParameterExpression.CreateThisParam(type2)),
                    Expression.Constant(true, TypeSignature.Boolean)
                )
            ));
            var type3 = TypeSignature.Class("MyType3", NamespaceSignature.Parse("MyNamespace"), Accessibility.APublic);
            cx.AddType(TypeDef.Empty(type3).AddMember(
                // Should be renamed
                new MethodDef(
                    MethodSignature.Instance("Equals", type3, Accessibility.APublic, TypeSignature.Boolean, new MethodParameter(TypeSignature.Object, "obj2")),
                    ImmutableArray.Create(ParameterExpression.CreateThisParam(type3), ParameterExpression.Create(TypeSignature.Object, "obj2")),
                    Expression.Constant(true, TypeSignature.Boolean)
                )
            ));
            var type4 = TypeSignature.Class("MyType4", NamespaceSignature.Parse("MyNamespace"), Accessibility.APublic);
            cx.AddType(TypeDef.Empty(type4).AddMember(
                // Should be renamed
                new MethodDef(
                    MethodSignature.Static("Equals", type4, Accessibility.APublic, TypeSignature.Boolean, new MethodParameter(TypeSignature.Object, "obj2")),
                    ImmutableArray.Create(ParameterExpression.Create(TypeSignature.Object, "obj2")),
                    Expression.Constant(true, TypeSignature.Boolean)
                )
            ));
            var type5 = TypeSignature.Class("MyType5", NamespaceSignature.Parse("MyNamespace"), Accessibility.APublic);
            cx.AddType(TypeDef.Empty(type5).AddMember(
                // OK, this is override
                new MethodDef(
                    MethodSignature.Instance("Equals", type5, Accessibility.APublic, TypeSignature.Boolean, new MethodParameter(TypeSignature.Object, "obj2")).With(isOverride: true, isVirtual: true),
                    ImmutableArray.Create(ParameterExpression.CreateThisParam(type5), ParameterExpression.Create(TypeSignature.Object, "obj2")),
                    Expression.Constant(true, TypeSignature.Boolean)
                )
            ));
            check.CheckOutput(cx);
        }
    }
}
