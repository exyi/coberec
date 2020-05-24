using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests
{
    public class GenericsTests
    {
        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext cx = MetadataContext.Create();

        [Fact]
        public void AutoProperties()
        {
            var ns = NamespaceSignature.Parse("MyNamespace");
            var t1 = new GenericParameter(Guid.NewGuid(), "T1");
            var t2 = new GenericParameter(Guid.NewGuid(), "T2");
            var type = TypeSignature.Class("MyType", ns, Accessibility.APublic, true, false, t1, t2);
            var td = TypeDef.Empty(type)
                     .AddAutoProperty("A", t1, Accessibility.APublic)
                     .AddAutoProperty("B", t2, Accessibility.APublic, isStatic: true)
                     .AddAutoProperty("C", TypeSignature.FromType(typeof(Dictionary<,>)).Specialize(t1, t2), Accessibility.APublic, isStatic: true, isReadOnly: false)
                     .AddAutoProperty("D", TypeReference.Tuple(t1, TypeSignature.String, t2), Accessibility.APublic, isReadOnly: false)
                     ;
            cx.AddType(td);
            check.CheckOutput(cx);
        }

        [Fact]
        public void NestedGenericTypes()
        {
            var ns = NamespaceSignature.Parse("MyNamespace");
            var t1 = new GenericParameter(Guid.NewGuid(), "T1");
            var t2 = new GenericParameter(Guid.NewGuid(), "T2");
            var rootType = TypeSignature.Class("MyType", ns, Accessibility.APublic, true, false, t1);
            var type = TypeSignature.Class("MyNestedType", rootType, Accessibility.APublic, true, false, t2);
            var td = TypeDef.Empty(type)
                     .AddAutoProperty("A", t1, Accessibility.APublic)
                     .AddAutoProperty("B", t2, Accessibility.APublic, isStatic: true)
                     .AddAutoProperty("C", TypeSignature.FromType(typeof(Dictionary<,>)).Specialize(t1, t2), Accessibility.APublic, isStatic: true, isReadOnly: false)
                     .AddAutoProperty("D", TypeReference.Tuple(t1, TypeSignature.String, t2), Accessibility.APublic, isReadOnly: false)
                     .AddAutoProperty("E", type.SpecializeByItself(), Accessibility.APublic, isReadOnly: false)
                     ;
            cx.AddType(TypeDef.Empty(rootType).AddMember(td));
            check.CheckOutput(cx);
        }


        [Fact]
        public void GenericMethodInGenericTypes()
        {
            var ns = NamespaceSignature.Parse("MyNamespace");
            var t1 = new GenericParameter(Guid.NewGuid(), "T1");
            var t2 = new GenericParameter(Guid.NewGuid(), "T2");
            var tresult = new GenericParameter(Guid.NewGuid(), "TResult");
            var rootType = TypeSignature.Class("MyType", ns, Accessibility.APublic, true, false, t1);

            var (f, p) = PropertyBuilders.CreateAutoProperty(rootType, "A", t1, isReadOnly: false);

            var map_fn_type = new FunctionType(new [] { new MethodParameter(t1, "a") }, tresult);
            var map_sgn = MethodSignature.Instance("Map", rootType, Accessibility.APublic,
                rootType.Specialize(tresult),
                new [] { tresult },
                new MethodParameter(map_fn_type.TryGetDelegate(), "func")
            );
            var tmp = ParameterExpression.Create(map_sgn.ResultType, "result");
            var map_def = MethodDef.Create(map_sgn, (@this, fn) =>
                Expression.Block(
                    new [] {
                        tmp.Ref()
                        .CallMethod(p.Setter.Signature.Specialize(new TypeReference[] { tresult }, null),
                            fn.Read().FunctionConvert(map_fn_type).Invoke(@this.Ref().CallMethod(p.Getter.Signature.SpecializeFromDeclaringType()))
                        )
                    },
                    result: tmp
                )
                .Where(tmp, Expression.NewObject(MethodSignature.ImplicitConstructor(rootType).Specialize(new TypeReference[] { tresult }, null)))
            );


            var type = TypeSignature.Class("MyNestedType", rootType, Accessibility.APublic, true, false, t2);

            var t3 = new GenericParameter(Guid.NewGuid(), "T3");
            var method = MethodSignature.Instance(
                "M", type, Accessibility.APublic,
                returnType: type.Specialize(t1, t3),
                typeParameters: new [] { t3 });

            var td = TypeDef.Empty(type)
                     .AddMember(MethodDef.Create(method, @this =>
                        Expression.NewObject(
                            MethodSignature.ImplicitConstructor(type).Specialize(new TypeReference[] { t1, t3 }, null),
                            ImmutableArray<Expression>.Empty
                        )
                     ))
                     ;
            cx.AddType(TypeDef.Empty(rootType).AddMember(td, f, p, map_def));
            check.CheckOutput(cx);
        }
    }
}
