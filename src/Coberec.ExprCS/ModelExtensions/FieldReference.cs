using System;
using System.Collections.Generic;
using System.Linq;
using R = System.Reflection;
using LE = System.Linq.Expressions;
using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    /// <summary> Represents a reference to a field. The generic parameters of the parent class are substituted - this class is basically <see cref="FieldSignature" /> + generic arguments </summary>
    public partial class FieldReference
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, FieldReference f)
        {
            if (f.Signature is null) return;
            var expectedCount = f.Signature.DeclaringType.TotalParameterCount();
            if (expectedCount != f.TypeParameters.Length)
                e.Add(ValidationErrors.Create($"Declaring type {f.Signature.DeclaringType} expected {expectedCount} parameters, got [{string.Join(", ", f.TypeParameters)}]"));
        }

        public SpecializedType DeclaringType() => new SpecializedType(this.Signature.DeclaringType, this.TypeParameters);
        public TypeReference ResultType() => Signature.ResultType.SubstituteGenerics(Signature.DeclaringType.AllTypeParameters(), this.TypeParameters);

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
                        throw new NotSupportedException($"Can't get field reference from member {memberExpr.Member}");
                default:
                    throw new NotSupportedException($"Can't get field reference from expression {b}");
            }
        }
    }
}
