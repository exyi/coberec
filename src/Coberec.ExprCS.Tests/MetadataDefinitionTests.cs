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
            var ns = new NamespaceSignature("MyNamespace", parent: null);
            var type = TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var typeDef = TypeDef.Empty(type);

            cx.AddType(typeDef);
            check.CheckOutput(cx);
        }

        [Fact]
        public void NestedTypesWithInheritance()
        {
            var ns = new NamespaceSignature("MyNamespace", parent: null);
            var rootType = TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var type1 = TypeSignature.Class("A", rootType, Accessibility.APublic);
            var type2 = type1.With(name: "B");
            var typeDef = TypeDef.Empty(rootType).With(members: ImmutableArray.Create<MemberDef>(
                new TypeDef(type1, null, ImmutableArray<SpecializedType>.Empty, ImmutableArray<MemberDef>.Empty),
                new TypeDef(type2, new SpecializedType(type1, ImmutableArray<TypeReference>.Empty), ImmutableArray<SpecializedType>.Empty, ImmutableArray<MemberDef>.Empty)
            ));

            cx.AddType(typeDef);

            cx.AddType(typeDef.With(signature: typeDef.Signature.With(name: "MyType2"), extends: new SpecializedType(type2, ImmutableArray<TypeReference>.Empty)));

            check.CheckOutput(cx);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IEquatableImplementation(bool isStruct)
        {
            var ns = new NamespaceSignature("MyNamespace", parent: null);
            var type = isStruct ? TypeSignature.Struct("MyType", ns, Accessibility.APublic)
                                : TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var iequatableT = cx.FindTypeDef(typeof(IEquatable<>));
            var method = new MethodSignature(type, ImmutableArray.Create(new MethodParameter(type, "obj")), "Equals", cx.FindType(typeof(bool)), false, Accessibility.APublic, false, false, false, false, ImmutableArray<GenericParameter>.Empty);
            var thisP = ParameterExpression.CreateThisParam(method);
            var methodDef = new MethodDef(method, ImmutableArray.Create(thisP), new ConstantExpression(true, cx.FindType(typeof(bool))));
            var typeDef = TypeDef.Empty(type).With(
                implements: ImmutableArray.Create(new SpecializedType(iequatableT, ImmutableArray.Create<TypeReference>(type))),
                members: ImmutableArray.Create<MemberDef>(methodDef));

            cx.AddType(typeDef);


            check.CheckOutput(cx, $"{(isStruct ? "struct" : "class")}");
        }

        [Fact]
        public void FewFields()
        {
            var stringT = cx.FindType(typeof(string));
            var stringArr = cx.FindType(typeof(string[]));
            var someTuple = cx.FindType(typeof((List<int>, System.Threading.Tasks.Task)));

            var ns = new NamespaceSignature("MyNamespace", parent: null);
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
    }
}
