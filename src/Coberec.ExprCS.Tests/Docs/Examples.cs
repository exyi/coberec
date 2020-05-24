using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests.Docs
{
    public class Examples
    {
        private TypeDef ImplementToString(TypeDef declaringType)
        {
            var properties =
                declaringType.Members
                .OfType<PropertyDef>()
                .Select(p => p.Signature)
                .Where(p => !p.IsStatic);

            var toStringSgn = MethodSignature.Override(declaringType.Signature, MethodSignature.Object_ToString);
            var toStringDef = MethodDef.Create(toStringSgn, @this => {

                Expression formatProperty(PropertySignature property)
                {
                    return @this.Read().ReadProperty(property);
                }

                IEnumerable<Expression> stringFragments()
                {
                    yield return Expression.Constant(declaringType.Signature.Name);
                    yield return Expression.Constant(" {");
                    var first = true;
                    foreach (var property in properties)
                    {
                        if (first)
                            first = false;
                        else
                            yield return Expression.Constant(",");
                        yield return Expression.Constant(" " + property.Name + " = ");
                        yield return formatProperty(property);
                    }
                    yield return Expression.Constant(" }");
                }

                return ExpressionFactory.String_Concat(stringFragments());
            });


            return declaringType.AddMember(toStringDef);
        }

        [Fact]
        public void ToStringExample()
        {
            var type = TypeSignature.Class("MyClass", NamespaceSignature.Parse("NS"), Accessibility.APublic);

            cx.AddType(ImplementToString(
                TypeDef.Empty(type)
                .AddAutoProperty("A", TypeSignature.String)
                .AddAutoProperty("B", TypeSignature.ValueTuple2.Specialize(TypeSignature.Boolean, TypeSignature.Boolean))
            ));

            cx.AddType(ImplementToString(
                TypeDef.Empty(type.With(name: "SecondClass"))
            ));

            check.CheckOutput(cx);
        }

        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create();
    }
}
