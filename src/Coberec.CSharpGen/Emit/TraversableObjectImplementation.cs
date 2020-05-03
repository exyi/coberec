using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TS=ICSharpCode.Decompiler.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using Coberec.CoreLib;
using Coberec.MetaSchema;
using M=Coberec.MetaSchema;
using E=Coberec.ExprCS;
using Coberec.ExprCS;
using Xunit;

namespace Coberec.CSharpGen.Emit
{
    public static class TraversableObjectImplementation
    {
        static TypeSignature ITraversableObjectType = TypeSignature.FromType(typeof(ITraversableObject));
        public static Func<E.TypeDef, E.TypeDef> ImplementTraversable(TypeSignature declaringType, (TypeField schema, FieldReference field)[] fields)
        {
            // ImmutableArray<string> Properties { get; }
            // int PropertyCount { get; }
            // object GetValue(int propIndex);

            var propertiesProperty = CreateProperties(declaringType, fields);
            var propertyCountProperty = CreatePropertyCount(declaringType, fields);
            var getValueMethod = CreateGetValue(declaringType, fields);

            return x => x.AddMember(propertiesProperty, propertyCountProperty, getValueMethod)
                         .AddImplements(ITraversableObjectType.Specialize());
        }

        static PropertyDef CreateProperties(TypeSignature declaringType, (TypeField schema, FieldReference field)[] fields)
        {
            var properties = fields.Select(f => f.schema.Name).ToImmutableArray();
            var sgn = PropertySignature.Create(
                "Coberec.CoreLib.ITraversableObject.Properties",
                declaringType,
                TypeSignature.ImmutableArrayOfT.Specialize(TypeSignature.String),
                Accessibility.APrivate,
                null);

            return new PropertyDef(sgn,
                getter: MethodDef.Create(sgn.Getter, @this =>
                {
                    return ExpressionFactory.MakeImmutableArray(TypeSignature.String, properties.EagerSelect(Expression.Constant));
                }),
                setter: null)
                .AddImplements(PropertyReference.FromLambda<ITraversableObject>(o => o.Properties));
        }

        static PropertyDef CreatePropertyCount(TypeSignature declaringType, (TypeField schema, FieldReference field)[] fields)
        {
            var sgn = PropertySignature.Create(
                "Coberec.CoreLib.ITraversableObject.PropertyCount",
                declaringType,
                TypeSignature.Int32,
                Accessibility.APrivate,
                null);

            return new PropertyDef(sgn,
                getter: MethodDef.Create(sgn.Getter, @this =>
                {
                    return Expression.Constant(fields.Length);
                }),
                setter: null)
                .AddImplements(PropertyReference.FromLambda<ITraversableObject>(o => o.PropertyCount));
        }

        static MemberDef CreateGetValue(TypeSignature declaringType, (TypeField schema, FieldReference field)[] fields)
        {
            var baseSgn = MethodReference.FromLambda<ITraversableObject>(o => o.GetValue(0));
            var sgn = MethodSignature.Override(declaringType, baseSgn.Signature).With(
                accessibility: Accessibility.APrivate,
                name: "Coberec.CoreLib.ITraversableObject.GetValue"
            );

            return MethodDef.Create(sgn, (@this, index) => {
                var propExpr = fields.Select((f, i) => (i, e: @this.Ref().ReadField(f.field).Box()));
                var expr = propExpr.Reverse().Aggregate(
                    Expression.Constant<object>(null),
                    (@else, x) => Expression.Conditional(
                        Expression.Binary("==", index, Expression.Constant(x.i)),
                        x.e,
                        @else
                    )
                );
                return expr;
            }).AddImplements(MethodReference.FromLambda<ITraversableObject>(t => t.GetValue(0)));
        }
    }
}
