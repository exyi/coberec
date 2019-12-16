using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests
{
    public class ValueTypeTests
    {
        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create("MyModule");

        static TypeSignature GuidType = TypeSignature.Struct("Guid", NamespaceSignature.System, Accessibility.APublic);
        static TypeSignature MyStruct = TypeSignature.Struct("MyStruct", NamespaceSignature.Parse("NS"), Accessibility.APublic);
        static FieldSignature MyStruct_GuidField = new FieldSignature(MyStruct, "id", Accessibility.APublic, GuidType, false, false);
        static FieldSignature MyStruct_IntField = new FieldSignature(MyStruct, "count", Accessibility.APublic, TypeSignature.Int32, false, false);
        static TypeDef MyStructDef = new TypeDef(
            MyStruct,
            extends: null,
            implements: ImmutableArray<SpecializedType>.Empty,
            members: ImmutableArray.Create<MemberDef>(
                new FieldDef(MyStruct_GuidField),
                new FieldDef(MyStruct_IntField)
            )
        );

        // TODO: validate MethodSignature: struct can not have parameterless .ctor, struct can not have abstract, virtual, override;
        static MethodSignature MyStruct_Method = new MethodSignature(MyStruct, ImmutableArray<MethodParameter>.Empty, "SomeMethod", TypeSignature.Void, false, Accessibility.APublic, false, false, false, false, ImmutableArray<GenericParameter>.Empty);

        TypeDef MyStructWithConstructor()
        {
            var methodSignature = MethodSignature.Constructor(MyStruct, Accessibility.APublic, new MethodParameter(GuidType, "id"), new MethodParameter(TypeSignature.Int32, "count"));

            var thisP = ParameterExpression.CreateThisParam(MyStruct);
            var idP = ParameterExpression.Create(GuidType, "id");
            var countP = ParameterExpression.Create(TypeSignature.Int32, "count");

            var method = MethodDef.Create(methodSignature, (thisP, idP, countP) =>
                Expression.Block(
                    ImmutableArray.Create(
                        Expression.ReferenceAssign(Expression.FieldAccess(MyStruct_GuidField, thisP), idP),
                        Expression.ReferenceAssign(Expression.FieldAccess(MyStruct_IntField, thisP), countP)
                    ),
                    result: Expression.Nop
                )
            );
            return MyStructDef.AddMember(method);
        }

        TypeDef MyStructWithMethod()
        {
            var thisP = ParameterExpression.CreateThisParam(MyStruct);

            var method = MethodDef.Create(MyStruct_Method, _ => Expression.Nop);
            return MyStructDef.AddMember(method);
        }

        [Fact]
        public void StructConstructor()
        {
            cx.AddType(MyStructWithConstructor());
            check.CheckOutput(cx);
        }

        private TypeReference OptionallyReference(TypeReference a, bool makeReference) =>
            makeReference ? TypeReference.ByReferenceType(a) : a;

        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void StructFieldAccess(bool reference, bool dereference)
        {
            cx.AddType(MyStructDef);

            var thisP = ParameterExpression.Create(OptionallyReference(MyStruct, reference), "this");

            var target = dereference ? Expression.Dereference(thisP) : thisP;
            var body = Expression.ReferenceAssign(
                Expression.FieldAccess(MyStruct_IntField, target),
                Expression.Constant(0, TypeSignature.Int32));

            cx.AddTestExpr(body, thisP);
            check.CheckOutput(cx, $"ref={reference}&deref={dereference}");
        }

        [Fact]
        public void StructReadonlyField()
        {
            var roField = new FieldSignature(MyStruct, "ROField", Accessibility.APublic, TypeSignature.Int32, false, true);
            cx.AddType(MyStructDef.AddMember(new FieldDef(roField)));

            var thisP = ParameterExpression.Create(TypeReference.ByReferenceType(MyStruct), "this");

            var body = Expression.Dereference(Expression.FieldAccess(roField, thisP));

            cx.AddTestExpr(body, thisP);
            check.CheckOutput(cx);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void StructMethodCall(bool reference, bool dereference)
        {
            cx.AddType(MyStructWithMethod());

            var thisP = ParameterExpression.Create(OptionallyReference(MyStruct, reference), "this");

            var target = dereference ? Expression.Dereference(thisP) : thisP;

            var body = Expression.MethodCall(MyStruct_Method, ImmutableArray<Expression>.Empty, target);

            cx.AddTestExpr(body, thisP);
            check.CheckOutput(cx, $"ref={reference}&deref={dereference}");
        }

        [Fact]
        public void StructBoxing()
        {
            cx.AddType(MyStructDef);
            var p = ParameterExpression.Create(MyStruct, "p");
            cx.AddTestExpr(Expression.ReferenceConversion(p, TypeSignature.Object), p);
            cx.AddTestExpr(Expression.MethodCall(
                cx.GetMemberMethods(TypeSignature.Object.NotGeneric()).Single(m => m.Signature.Name == "GetHashCode"),
                args: ImmutableArray<Expression>.Empty,
                Expression.ReferenceConversion(p, TypeSignature.Object)
            ), p);
            check.CheckOutput(cx);
        }

        [Fact]
        public void StructInterface()
        {
            var icloneable = TypeSignature.FromType(typeof(ICloneable));
            var method = new MethodSignature(MyStruct, ImmutableArray<MethodParameter>.Empty, "Clone", TypeSignature.Object, false, Accessibility.APublic, false, false, false, false, ImmutableArray<GenericParameter>.Empty);
            var thisP = ParameterExpression.CreateThisParam(method);
            var methodDef = new MethodDef(method, ImmutableArray.Create(thisP), Expression.ReferenceConversion(thisP, TypeSignature.Object));

            cx.AddType(MyStructDef.AddMember(methodDef).AddImplements(new SpecializedType(icloneable, ImmutableArray<TypeReference>.Empty)));

            var interfaceClone = cx.GetMemberMethods(icloneable.NotGeneric()).Single(m => m.Signature.Name == "Clone");

            var p = ParameterExpression.Create(MyStruct, "p");
            cx.AddTestExpr(Expression.MethodCall(method, ImmutableArray<Expression>.Empty, p), p);
            cx.AddTestExpr(Expression.MethodCall(interfaceClone, ImmutableArray<Expression>.Empty, Expression.ReferenceConversion(p, icloneable)), p);

            check.CheckOutput(cx);
        }
    }
}
