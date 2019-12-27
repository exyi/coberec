using System;
using System.Collections.Generic;
using System.Linq;
using R = System.Reflection;
using LE = System.Linq.Expressions;

namespace Coberec.ExprCS
{
    /// <summary> Represents a reference to a field. The generic parameters of the parent class are substituted - this class is basically <see cref="FieldSignature" /> + generic arguments </summary>
    public partial class FieldReference
    {
        public SpecializedType DeclaringType() => new SpecializedType(this.Signature.DeclaringType, this.TypeParameters);
        public TypeReference ResultType() => Signature.ResultType.SubstituteGenerics(Signature.DeclaringType.TypeParameters, this.TypeParameters);

        public override string ToString() => FieldSignature.ToString(Signature, ResultType());

        public static FieldReference FromReflection(R.FieldInfo field)
        {
            var s = FieldSignature.FromReflection(field);
            var declaringType = ((TypeReference.SpecializedTypeCase) TypeReference.FromType(field.DeclaringType)).Item;
            return new FieldReference(s, declaringType.GenericParameters);
        }

        /// <summary> Gets the top most accessed field from the expression. For example `(X a) => a.B.C.D` will return descriptor of the field `D`. </summary>
        public static FieldReference FromLambda<T>(LE.Expression<Func<T, object>> expr)
        {
            var b = expr.Body;
            while (b is LE.UnaryExpression uExpr && uExpr.NodeType == LE.ExpressionType.Convert)
                b = uExpr.Operand;

            switch (b)
            {
                case LE.MemberExpression memberExpr:
                    if (memberExpr.Member is R.FieldInfo field)
                        return FromReflection(field);
                    else
                        throw new NotSupportedException($"Can't get method reference from member {memberExpr.Member}");
                default:
                    throw new NotSupportedException($"Can't get method reference from expression {b}");
            }
        }
    }
}
