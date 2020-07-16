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
        readonly OutputChecker check = new OutputChecker("testoutput", sanitizeGuids: true);
        readonly MetadataContext cx = MetadataContext.Create();
        static readonly NamespaceSignature ns = NamespaceSignature.Parse("MyNamespace");

        [Fact]
        public void OneEmptyType()
        {
            var type = TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var typeDef = TypeDef.Empty(type);

            cx.AddType(typeDef);
            check.CheckOutput(cx);
        }

        [Fact]
        public void NestedTypesWithInheritance()
        {
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
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public void IEquatableImplementation(bool isStruct, bool isExplicit)
        {
            var type = isStruct ? TypeSignature.Struct("MyType", ns, Accessibility.APublic)
                                : TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var iequatableT = TypeSignature.FromType(typeof(IEquatable<>)).Specialize(type);
            var interfaceMethod = cx.GetMemberMethods(iequatableT, "Equals").Single();
            var method = new MethodSignature(type, ImmutableArray.Create(new MethodParameter(type, "obj")), "Equals", TypeReference.FromType(typeof(bool)), false, isExplicit ? Accessibility.APrivate : Accessibility.APublic, false, false, false, false, ImmutableArray<GenericParameter>.Empty);
            var methodDef = MethodDef.Create(method, (_thisP, _objP) => new ConstantExpression(true, TypeReference.FromType(typeof(bool))))
                                     .AddImplements(interfaceMethod);
            var typeDef = TypeDef.Empty(type).With(
                implements: ImmutableArray.Create(iequatableT),
                members: ImmutableArray.Create<MemberDef>(methodDef));

            cx.AddType(typeDef);


            check.CheckOutput(cx, $"{(isStruct ? "struct" : "class")}{(isExplicit ? "-explicit" : "")}");
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
            var type = TypeSignature.Interface("MyInterface2", ns, Accessibility.APublic);

            var method1 = MethodSignature.Instance("StringMethod", type, Accessibility.APublic, TypeSignature.Int32, new MethodParameter(TypeSignature.String, "myParameter").WithDefault("default value"));
            var method2 = MethodSignature.Instance("ValueTypeMethod", type, Accessibility.APublic, TypeSignature.Int32, new MethodParameter(TypeSignature.FromType(typeof(Guid)), "myParameter").WithDefault(null));
            var typeDef = TypeDef.Empty(type)
                .AddMember(MethodDef.InterfaceDef(method1))
                .AddMember(MethodDef.InterfaceDef(method2));


            cx.AddType(typeDef);
            check.CheckOutput(cx);
        }

        [Fact]
        public void StandardProperties()
        {
            // TODO: remove those CompilerGenerated attributes
            var type = TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var prop = PropertySignature.Create("A", type, TypeSignature.String, Accessibility.APublic, Accessibility.AProtected);
            var td = TypeDef.Empty(type).AddMember(
                new PropertyDef(prop,
                    getter: MethodDef.Create(prop.Getter, thisP => Expression.Constant("abcd")),
                    setter: MethodDef.Create(prop.Setter, (thisP, xP) =>
                        Expression.While(FluentExpression.Box(thisP).CallMethod(MethodSignature.Object_Equals, Expression.Default(TypeSignature.Object)), Expression.Nop))
                )
            );
            cx.AddType(td);
            check.CheckOutput(cx);
        }

        [Fact]
        public void AutoProperties()
        {
            var type = TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var td = TypeDef.Empty(type)
                     .AddAutoProperty("A", TypeSignature.String, Accessibility.APublic)
                     .AddAutoProperty("B", TypeSignature.String, Accessibility.APublic, isStatic: true)
                     .AddAutoProperty("C", TypeSignature.String, Accessibility.APublic, isStatic: true, isReadOnly: false)
                     .AddAutoProperty("D", TypeSignature.String, Accessibility.APublic, isReadOnly: false)
                     .AddAutoProperty("E", TypeSignature.TimeSpan, Accessibility.APublic)
                     .AddAutoProperty("F", type, Accessibility.APublic)
                     ;
            cx.AddType(td);
            check.CheckOutput(cx);

            check.CheckString(td.ToString());
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        [InlineData(true, false, true)]
        public void Overrides(bool isInterface, bool isSealed, bool isAbstract)
        {
            var mGenerics = Enumerable.Range(0, 2).Select(i => GenericParameter.Create("U" + i)).ToArray();
            var baseType = isInterface ? TypeSignature.Interface("Base", ns, Accessibility.APublic)
                                       : TypeSignature.Class("BaseC", ns, Accessibility.APublic, isAbstract: true);
            
            var baseMethod1 = MethodSignature.Abstract("M1", baseType, isInterface ? Accessibility.APublic : Accessibility.AProtected, TypeSignature.Object).With(isAbstract: isInterface);
            var baseMethod2 = MethodSignature.Abstract("M2", baseType, Accessibility.APublic, mGenerics[0], new MethodParameter(mGenerics[1], "a")).With(typeParameters: mGenerics.ToImmutableArray());
            var baseProperty = PropertySignature.Abstract("P1", baseType, TypeSignature.Boolean, Accessibility.APublic, null);

            cx.AddType(TypeDef.Empty(baseType).AddMember(
                isInterface ? MethodDef.InterfaceDef(baseMethod1) : MethodDef.Create(baseMethod1, _ => Expression.Constant<object>(null)),
                MethodDef.InterfaceDef(baseMethod2),
                PropertyDef.InterfaceDef(baseProperty)
            ));

            var type = TypeSignature.Class("C", ns, Accessibility.APublic, canOverride: !isSealed, isAbstract: isAbstract);
            var method1 = MethodSignature.Override(type, baseMethod1, isAbstract: isAbstract);
            var method2 = MethodSignature.Override(type, baseMethod2);
            var property = PropertySignature.Override(type, baseProperty);

            cx.AddType(TypeDef.Empty(type, isInterface ? null : baseType.Specialize())
                .AddImplements(isInterface ? new [] { baseType.Specialize() } : new SpecializedType[0])
                .AddMember(
                    isAbstract ? MethodDef.InterfaceDef(method1) : MethodDef.Create(method1, _ => Expression.Constant(1).Box()),
                    MethodDef.Create(method2, (_, __) => Expression.Default(method2.TypeParameters[0])),
                    PropertyDef.Create(property, @this => Expression.Default(property.Type))
                ));

            check.CheckOutput(cx, $"ifc({isInterface})-sealed({isSealed})-abstract({isAbstract})");
        }

        [Fact]
        public void NameSanitization()
        {
            var stringT = TypeReference.FromType(typeof(string));

            var type = TypeSignature.Class("MyType", ns, Accessibility.APublic);
            cx.AddType(TypeDef.Empty(type).AddMember(
                // Should be renamed, there is collision with virtual object.Equals
                new FieldDef(new FieldSignature(type, "Equals", Accessibility.APublic, stringT, false, true))
            ));
            var type2 = TypeSignature.Class("MyType2", ns, Accessibility.APublic);
            cx.AddType(TypeDef.Empty(type2).AddMember(
                // OK, no collision here
                new MethodDef(
                    MethodSignature.Instance("Equals", type2, Accessibility.APublic, TypeSignature.Boolean),
                    ImmutableArray.Create(ParameterExpression.CreateThisParam(type2)),
                    Expression.Constant(true)
                )
            ));
            var type3 = TypeSignature.Class("MyType3", ns, Accessibility.APublic);
            cx.AddType(TypeDef.Empty(type3).AddMember(
                // Should be renamed
                new MethodDef(
                    MethodSignature.Instance("Equals", type3, Accessibility.APublic, TypeSignature.Boolean, new MethodParameter(TypeSignature.Object, "obj2")),
                    ImmutableArray.Create(ParameterExpression.CreateThisParam(type3), ParameterExpression.Create(TypeSignature.Object, "obj2")),
                    Expression.Constant(true)
                )
            ));
            var type4 = TypeSignature.Class("MyType4", ns, Accessibility.APublic);
            cx.AddType(TypeDef.Empty(type4).AddMember(
                // Should be renamed
                new MethodDef(
                    MethodSignature.Static("Equals", type4, Accessibility.APublic, TypeSignature.Boolean, new MethodParameter(TypeSignature.Object, "obj2")),
                    ImmutableArray.Create(ParameterExpression.Create(TypeSignature.Object, "obj2")),
                    Expression.Constant(true)
                )
            ));
            var type5 = TypeSignature.Class("MyType5", ns, Accessibility.APublic);
            cx.AddType(TypeDef.Empty(type5).AddMember(
                // OK, this is override
                new MethodDef(
                    MethodSignature.Instance("Equals", type5, Accessibility.APublic, TypeSignature.Boolean, new MethodParameter(TypeSignature.Object, "obj2")).With(isOverride: true, isVirtual: true),
                    ImmutableArray.Create(ParameterExpression.CreateThisParam(type5), ParameterExpression.Create(TypeSignature.Object, "obj2")),
                    Expression.Constant(true)
                )
            ));
            check.CheckOutput(cx);
        }

        [Fact]
        public void Doccomments()
        {
            var type = TypeSignature.Class("MyType", ns, Accessibility.APublic);
            var td = TypeDef.Empty(type)
                     .With(doccomment: new XmlComment("<summary> My type </summary>"))
                     .AddAutoProperty("A", TypeSignature.String, Accessibility.APublic, doccomment: new XmlComment("<summary> My property </summary>"))
                     ;

            var fieldSgn = new FieldSignature(type, "Field", Accessibility.AInternal, TypeSignature.Int16, false, false);
            td = td.AddMember(
                new FieldDef(fieldSgn, new XmlComment("<summary> My field </summary>"))
            );
            cx.AddType(td);
            check.CheckOutput(cx);
        }
    }
}
