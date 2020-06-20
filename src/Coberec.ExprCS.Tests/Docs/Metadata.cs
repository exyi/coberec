using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests.Docs
{
    public class Metadata
    {
        [Fact]
        public void GenericClass()
        {
            var paramT = GenericParameter.Create("T");
            var myContainerSgn = TypeSignature.Class(
                "MyContainer",
                NamespaceSignature.Parse("NS"),
                Accessibility.APublic,
                genericParameters: new [] { paramT }
            );

            var (item_field, item_prop) = PropertyBuilders.CreateAutoProperty(
                myContainerSgn,
                name: "Item",
                propertyType: paramT,
                isReadOnly: false
            );

            var listType = TypeSignature.FromType(typeof(List<>))
                           .Specialize(paramT);
            var toListSgn = MethodSignature.Instance(
                "ToList",
                myContainerSgn,
                Accessibility.APublic,
                returnType: listType
            );

            var toListDef = MethodDef.Create(toListSgn, thisParam => {

                var resultVar = ParameterExpression.Create(listType, "result");
                var listCtor = MethodReference.FromLambda(() => new List<int>())
                               .Signature
                               .Specialize(paramT);
                var listAdd = MethodReference.FromLambda<List<int>>(l => l.Add(0))
                              .Signature
                              .Specialize(paramT);

                return Expression.LetIn(
                    // result = new List<T>()
                    resultVar, Expression.NewObject(listCtor),
                    new [] {
                        // result.Add(this.Item)
                        resultVar.Read().CallMethod(listAdd,
                            thisParam.Read().ReadField(item_field.Signature.SpecializeFromDeclaringType())
                        )
                    }.ToBlock(resultVar)
                );
            });

            var copyFromSgn = MethodSignature.Instance(
                "CopyFrom",
                myContainerSgn,
                Accessibility.APublic,
                returnType: TypeSignature.Void,
                new MethodParameter(
                    myContainerSgn.SpecializeByItself(),
                    "other"
                )
            );

            var copyFromDef = MethodDef.Create(copyFromSgn, (thisParam, otherParam) => {
                var field = item_field.Signature.SpecializeFromDeclaringType();
                return thisParam.Read().AssignField(
                    field,
                    otherParam.Read().ReadField(field)
                );
            });


            var myContainerDef =
                TypeDef.Empty(myContainerSgn)
                .AddMember(item_field, item_prop, toListDef, copyFromDef);

            cx.AddType(myContainerDef);

            check.CheckOutput(cx);
        }

        [Fact]
        public void TypeCheatsheet()
        {
            void addType(TypeSignature type, params MemberDef[] defs) =>
                cx.AddType(TypeDef.Empty(type).AddMember(defs));

            var ns = NamespaceSignature.Parse("MyNamespace");
            var @public = Accessibility.APublic;

            var justClass = TypeSignature.Class("JustClass", ns, Accessibility.AInternal);
            addType(justClass);
            addType(TypeSignature.Class("PublicClass", ns, @public));
            addType(TypeSignature.Class("AbstractClass", ns, @public, isAbstract: true));
            addType(TypeSignature.SealedClass("SealedClass", ns, @public));
            addType(TypeSignature.StaticClass("StaticClass", ns, @public));
            addType(TypeSignature.Struct("Struct", ns, @public));
            var myInterface = TypeSignature.Interface("MyInterface", ns, @public);
            addType(myInterface);
            var paramT = GenericParameter.Create("T");
            addType(TypeSignature.Class("GenericClass", ns, @public, genericParameters: new [] { paramT }));

            cx.AddType(
                TypeDef.Empty(
                    TypeSignature.Class("Overrides", ns, @public),
                    extends: justClass.Specialize()
                )
            );

            cx.AddType(
                TypeDef.Empty(TypeSignature.Class("Implements", ns, @public))
                       .AddImplements(myInterface.Specialize())
            );

            check.CheckOutput(cx);
        }

        [Fact]
        public void MethodCheatsheet()
        {
            var @public = Accessibility.APublic;
            var declType = TypeSignature.Class("MyClass", NamespaceSignature.Parse("MyNamespace"), @public, isAbstract: true);
            var instanceMethod = MethodSignature.Instance("InstanceMethod", declType, @public, returnType: TypeSignature.Void);
            var staticMethod = MethodSignature.Static("StaticMethod", declType, @public, returnType: TypeSignature.Void);
            var abstractMethod = MethodSignature.Abstract("AbstractMethod", declType, @public, returnType: TypeSignature.Void);
            var virtualMethod = MethodSignature.Virtual("VirtualMethod", declType, @public, returnType: TypeSignature.Void);

            var paramT = GenericParameter.Create("T");
            var genericMethod = MethodSignature.Static("GenericMethod", declType, @public, returnType: paramT, new [] { paramT });

            var parameters = new [] {
                new MethodParameter(TypeSignature.String, "p1"),
                new MethodParameter(TypeReference.ByReferenceType(TypeSignature.Double), "byReferenceParameter"),
                new MethodParameter(TypeSignature.Int32, "withDefaultValue").WithDefault(0)
            };
            var withParameters = MethodSignature.Instance("MethodWithParams", declType, @public, returnType: TypeSignature.Void, parameters);

            MethodDef emptyMethod(MethodSignature method) => MethodDef.CreateWithArray(method, args => Expression.Default(method.ResultType));

            cx.AddType(
                TypeDef.Empty(declType)
                .AddMember(
                    emptyMethod(instanceMethod),
                    emptyMethod(staticMethod),
                    MethodDef.InterfaceDef(abstractMethod),
                    emptyMethod(virtualMethod),
                    emptyMethod(genericMethod),
                    emptyMethod(withParameters)
                )
            );

            check.CheckOutput(cx);
        }

        [Fact]
        public void FieldCheatsheet()
        {
            var @public = Accessibility.APublic;
            var declType = TypeSignature.Class("MyClass", NamespaceSignature.Parse("MyNamespace"), @public);

            var instanceRO = FieldSignature.Instance("Instance", declType, @public, returnType: TypeSignature.Int32);
            var instanceMut = FieldSignature.Instance("InstanceMut", declType, @public, returnType: TypeSignature.Int32, isReadonly: false);
            var staticRO = FieldSignature.Static("Static", declType, @public, returnType: TypeSignature.Int32);
            var staticMut = FieldSignature.Static("StaticMut", declType, @public, returnType: TypeSignature.Int32, isReadonly: false);

            cx.AddType(
                TypeDef.Empty(declType)
                .AddMember(
                    new FieldDef(instanceMut),
                    new FieldDef(instanceRO),
                    new FieldDef(staticMut),
                    new FieldDef(staticRO)
                )
            );
            check.CheckOutput(cx);
        }

        [Fact]
        public void PropertyCheatsheet()
        {
            PropertyDef emptyProp(PropertySignature prop) => PropertyDef.Create(prop, @this => Expression.Default(prop.Type), (@this, value) => Expression.Nop);
            PropertyDef emptyStaticProp(PropertySignature prop) => PropertyDef.CreateStatic(prop, Expression.Default(prop.Type), value => Expression.Nop);
            var @public = Accessibility.APublic;
            var declType = TypeSignature.Class("MyClass", NamespaceSignature.Parse("MyNamespace"), @public, isAbstract: true);

            var instanceGS = PropertySignature.Instance("InstanceGS", declType, TypeSignature.Int32, getter: @public, setter: @public);
            var instanceG = PropertySignature.Instance("InstanceG", declType, TypeSignature.Int32, getter: @public, setter: null);
            var instanceS = PropertySignature.Instance("InstanceS", declType, TypeSignature.Int32, getter: null, setter: @public);
            var staticG = PropertySignature.Static("StaticG", declType, TypeSignature.Int32, getter: @public, setter: null);
            var staticGS = PropertySignature.Static("StaticGS", declType, TypeSignature.Int32, getter: @public, setter: Accessibility.AInternal);
            var abstractG = PropertySignature.Abstract("AbstractG", declType, TypeSignature.Int32, getter: @public);

            cx.AddType(
                TypeDef.Empty(declType)
                .AddMember(
                    emptyProp(instanceG),
                    emptyProp(instanceGS),
                    emptyProp(instanceS),
                    emptyStaticProp(staticG),
                    emptyStaticProp(staticGS),
                    PropertyDef.InterfaceDef(abstractG)
                )
            );
            check.CheckOutput(cx);
        }

        [Fact]
        public void AutoPropertyCheatsheet()
        {
            var @public = Accessibility.APublic;
            var declType = TypeSignature.Class("MyClass", NamespaceSignature.Parse("MyNamespace"), @public, isAbstract: true);

            var (p1, f1) = PropertyBuilders.CreateAutoProperty(declType, "P1", TypeSignature.Int32);
            var (p2, f2) = PropertyBuilders.CreateAutoProperty(declType, "P2", TypeSignature.Int32, accessibility: Accessibility.AProtected);
            var (p3, f3) = PropertyBuilders.CreateAutoProperty(declType, "P3", TypeSignature.Int32, isReadOnly: false);
            var (p4, f4) = PropertyBuilders.CreateAutoProperty(declType, "P4", TypeSignature.Int32, isStatic: true);

            cx.AddType(
                TypeDef.Empty(declType)
                .AddMember(
                    p1, f1,
                    p2, f2,
                    p3, f3,
                    p4, f4
                )
            );
            check.CheckOutput(cx);
        }

        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create();
    }
}
