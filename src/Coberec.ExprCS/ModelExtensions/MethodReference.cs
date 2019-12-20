using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;
using Xunit;
using LE = System.Linq.Expressions;
using SR = System.Reflection;

namespace Coberec.ExprCS
{
    /// <summary> Represents a reference to a method. The generic parameters of the parent class and the method are substituted - this class is basically <see cref="MethodSignature" /> + generic arguments </summary>
    public partial class MethodReference
    {
        public SpecializedType DeclaringType() => new SpecializedType(this.Signature.DeclaringType, this.TypeParameters);
        public TypeReference ResultType() => Signature.ResultType.SubstituteGenerics(Signature.TypeParameters, this.MethodParameters);
        public ImmutableArray<MethodParameter> Params() =>
            Signature.Params.EagerSelect(p => p.SubstituteGenerics(Signature.TypeParameters, this.MethodParameters)
                                               .SubstituteGenerics(Signature.DeclaringType.TypeParameters, this.TypeParameters));
        public string Name() => Signature.Name;

        public override string ToString() =>
            MethodSignature.ToString(Signature, this.MethodParameters, this.Params(), this.ResultType());

        public static MethodReference FromReflection(System.Reflection.MethodInfo method)
        {
            Assert.False(method.IsGenericMethodDefinition);
            var signature = MethodSignature.FromReflection(method);
            var declaringType = ((TypeReference.SpecializedTypeCase) TypeReference.FromType(method.DeclaringType)).Item;
            var methodArgs = method.IsGenericMethod ?
                             method.GetGenericArguments().EagerSelect(TypeReference.FromType) :
                             ImmutableArray<TypeReference>.Empty;
            return new MethodReference(signature, declaringType.GenericParameters, methodArgs);
        }

        /// <summary> Gets the top most invoked method from the expression. For example `(String a) => a.Trim(anything)` will return descriptor of the Trim method. The function also supports properties (it will return the getter). </summary>
        public static MethodReference FromLambda<T>(LE.Expression<Func<T, object>> expr)
        {
            var b = expr.Body;
            while (b is LE.UnaryExpression uExpr && uExpr.NodeType == LE.ExpressionType.Convert)
                b = uExpr.Operand;

            switch (b)
            {
                case LE.MethodCallExpression methodCall:
                    return FromReflection(methodCall.Method);

                case LE.MemberExpression memberExpr:
                    if (memberExpr.Member is SR.PropertyInfo prop)
                        return FromReflection(prop.GetMethod);
                    else
                        throw new NotSupportedException($"Can't get method reference from member {memberExpr.Member}");
                default:
                    throw new NotSupportedException($"Can't get method reference from member {b}");
            }
        }
    }
}
