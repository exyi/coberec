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

            var properties = fields.Select(f => f.schema.Name).ToImmutableArray();

            var propertiesProperty = CreateProperties(declaringType, _ => properties.EagerSelect(Expression.Constant));
            var propertyCountProperty = CreatePropertyCount(declaringType, fields.Length);
            var getValueMethod = CreateCompositeGetValue(declaringType, fields);

            return x => x.AddMember(propertiesProperty, propertyCountProperty, getValueMethod)
                         .AddImplements(ITraversableObjectType.Specialize());
        }

        public static PropertySignature UnionRawItem(TypeSignature declaringType, bool isPublic) =>
            PropertySignature.Abstract(
                "RawItem",
                declaringType,
                TypeSignature.Object,
                getter: isPublic ? Accessibility.APublic : Accessibility.AProtected,
                setter: null
            );

        public static PropertySignature UnionCaseName(TypeSignature declaringType, bool isPublic) =>
            PropertySignature.Abstract(
                "CaseName",
                declaringType,
                TypeSignature.String,
                getter: isPublic ? Accessibility.APublic : Accessibility.AProtected,
                setter: null
            );

        public static Func<E.TypeDef, E.TypeDef> ImplementTraversableUnion(TypeSignature declaringType)
        {
            var caseName = UnionCaseName(declaringType, isPublic: true);
            var rawItem = UnionCaseName(declaringType, isPublic: true);

            var propertyCountProperty = CreatePropertyCount(declaringType, 1);
            var propertiesProperty = CreateProperties(declaringType, @this => ImmutableArray.Create(
                @this.Ref().ReadProperty(caseName)
            ));
            var getValueMethod = CreateGetValue(declaringType, (@this, _) =>
                @this.Ref().ReadProperty(rawItem).Box()
            );

            return r => r.AddMember(
                PropertyDef.InterfaceDef(caseName),
                PropertyDef.InterfaceDef(rawItem),
                propertyCountProperty,
                propertiesProperty,
                getValueMethod
            );
        }

        public static Func<E.TypeDef, E.TypeDef> ImplementTraversableUnionCase(TypeSignature baseType, TypeSignature declaringType, FieldSignature itemField, string name)
        {
            var caseNameBase = UnionCaseName(baseType, isPublic: true);
            var rawItemBase = UnionCaseName(baseType, isPublic: true);

            var caseName = PropertyDef.Create(
                PropertySignature.Override(declaringType, caseNameBase),
                @this => Expression.Constant(name)
            );

            var rawItem = PropertyDef.Create(
                PropertySignature.Override(declaringType, rawItemBase),
                @this => @this.Ref().ReadField(itemField)
            );
            

            return r => r.AddMember(
                caseName,
                rawItem
            );
        }



        static PropertyDef CreateProperties(TypeSignature declaringType, Func<ParameterExpression, ImmutableArray<Expression>> fields)
        {
            var sgn = PropertySignature.Create(
                "Coberec.CoreLib.ITraversableObject.Properties",
                declaringType,
                TypeSignature.ImmutableArrayOfT.Specialize(TypeSignature.String),
                Accessibility.APrivate,
                null);

            return new PropertyDef(sgn,
                getter: MethodDef.Create(sgn.Getter, @this =>
                {
                    return ExpressionFactory.MakeImmutableArray(TypeSignature.String, fields(@this));
                }),
                setter: null)
                .AddImplements(PropertyReference.FromLambda<ITraversableObject>(o => o.Properties));
        }

        static PropertyDef CreatePropertyCount(TypeSignature declaringType, int count)
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
                    return Expression.Constant(count);
                }),
                setter: null)
                .AddImplements(PropertyReference.FromLambda<ITraversableObject>(o => o.PropertyCount));
        }

        static MemberDef CreateCompositeGetValue(TypeSignature declaringType, (TypeField schema, FieldReference field)[] fields)
        {
            return CreateGetValue(declaringType, (@this, index) => {
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
            });
        }
        static MemberDef CreateGetValue(TypeSignature declaringType, Func<ParameterExpression, ParameterExpression, Expression> body)
        {
            var baseSgn = MethodReference.FromLambda<ITraversableObject>(o => o.GetValue(0));
            var sgn = MethodSignature.Override(declaringType, baseSgn.Signature).With(
                accessibility: Accessibility.APrivate,
                name: "Coberec.CoreLib.ITraversableObject.GetValue"
            );

            return MethodDef.Create(sgn, body).AddImplements(MethodReference.FromLambda<ITraversableObject>(t => t.GetValue(0)));
        }
    }
}
