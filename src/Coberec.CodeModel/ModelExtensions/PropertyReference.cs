using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using LE = System.Linq.Expressions;
using R = System.Reflection;

namespace Coberec.ExprCS
{
    public partial class PropertyReference
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, PropertyReference p)
        {
            if (p.Signature is null) return;
            var expectedCount = p.Signature.DeclaringType.TotalParameterCount();
            if (expectedCount != p.TypeArguments.Length)
                e.Add(ValidationErrors.Create($"Type {p.Signature.DeclaringType} expected {expectedCount} parameters, got [{string.Join(", ", p.TypeArguments)}]"));
        }

        public SpecializedType DeclaringType() => new SpecializedType(this.Signature.DeclaringType, this.TypeArguments);
        public TypeReference Type() => Signature.Type.SubstituteGenerics(Signature.DeclaringType.AllTypeParameters(), this.TypeArguments);
        public MethodReference Getter() =>
            Signature.Getter == null ? null :
            new MethodReference(Signature.Getter, this.TypeArguments, ImmutableArray<TypeReference>.Empty);
        public MethodReference Setter() =>
            Signature.Setter == null ? null :
            new MethodReference(Signature.Setter, this.TypeArguments, ImmutableArray<TypeReference>.Empty);
        public string Name() => Signature.Name;

        public static PropertyReference FromReflection(R.PropertyInfo prop)
        {
            var signature = PropertySignature.FromReflection(prop);
            var declaringType = ((TypeReference.SpecializedTypeCase) TypeReference.FromType(prop.DeclaringType)).Item;
            return new PropertyReference(signature, declaringType.TypeArguments);
        }

        //// <summary> Gets the top most accessed property from the expression. For example `(X a) => a.B.C.D` will return descriptor of the property `D`. </summary>
        public static PropertyReference FromLambda<T>(LE.Expression<Func<T, object>> expr)
        {
            var b = expr.Body;
            while (b is LE.UnaryExpression uExpr && uExpr.NodeType == LE.ExpressionType.Convert)
                b = uExpr.Operand;

            switch (b)
            {
                case LE.MemberExpression memberExpr:
                    if (memberExpr.Member is R.PropertyInfo prop)
                        return FromReflection(prop);
                    else
                        throw new NotSupportedException($"Can't get property reference from member {memberExpr.Member}");
                default:
                    throw new NotSupportedException($"Can't get property reference from expression {b}");
            }
        }
    }
}
