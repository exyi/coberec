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

        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create("MyModule");
    }
}
