using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using Coberec.Utils;
using Xunit;
using LE = System.Linq.Expressions;
using R = System.Reflection;

namespace Coberec.ExprCS
{
    public partial class MethodReference
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, MethodReference m)
        {
            if (m.Signature is null) return;
            var expectedCount = m.Signature.DeclaringType.TotalParameterCount();
            if (expectedCount != m.TypeArguments.Length)
                e.Add(ValidationErrors.Create($"Type {m.Signature.DeclaringType} expected {expectedCount} parameters, got [{string.Join(", ", m.TypeArguments)}]"));
            if (m.Signature.TypeParameters.Length != m.MethodTypeArguments.Length)
                e.Add(ValidationErrors.Create($"Method {m.Signature} expected {expectedCount} type parameters, got [{string.Join(", ", m.MethodTypeArguments)}]"));
        }

        /// <summary> Method declaring type. Generic type parameters are substituted according to the type arguments. </summary>
        /// <seealso cref="MethodSignature.DeclaringType" />
        public SpecializedType DeclaringType() => new SpecializedType(this.Signature.DeclaringType, this.TypeArguments);
        /// <summary> Method result type. Generic type parameters are substituted according to the type arguments. </summary>
        /// <seealso cref="MethodSignature.ResultType" />
        public TypeReference ResultType() => Signature.ResultType.SubstituteGenerics(Signature.TypeParameters, this.MethodTypeArguments).SubstituteGenerics(Signature.DeclaringType.AllTypeParameters(), this.TypeArguments);
        /// <summary> Method parameters. Generic type parameters are substituted according to the type arguments. </summary>
        /// <seealso cref="MethodSignature.Params" />
        public ImmutableArray<MethodParameter> Params() =>
            Signature.Params.EagerSelect(p => p.SubstituteGenerics(Signature.TypeParameters, this.MethodTypeArguments)
                                               .SubstituteGenerics(Signature.DeclaringType.AllTypeParameters(), this.TypeArguments));
        public string Name() => Signature.Name;

        public FmtToken Format() =>
            MethodSignature.Format(Signature, this.MethodTypeArguments, this.Params(), this.ResultType());

        /// <summary> Creates a MethodReference from System.Reflections MethodInfo or ConstructorInfo. Also works for property getters, setters, static constructors, ... </summary>
        public static MethodReference FromReflection(R.MethodBase method)
        {
            Assert.False(method.IsGenericMethodDefinition);
            var signature = MethodSignature.FromReflection(method);
            var declaringType = ((TypeReference.SpecializedTypeCase) TypeReference.FromType(method.DeclaringType)).Item;
            var methodArgs = method.IsGenericMethod ?
                             method.GetGenericArguments().EagerSelect(TypeReference.FromType) :
                             ImmutableArray<TypeReference>.Empty;
            return new MethodReference(signature, declaringType.TypeArguments, methodArgs);
        }

        /// <summary> Gets the top most invoked method from the expression. For example `(String a) => a.Trim(anything)` will return descriptor of the Trim method. The function also supports properties (it will return the getter) and constuctors (using the `new XXX()` syntax). </summary>
        public static MethodReference FromLambda<T>(LE.Expression<Func<T, object>> expr) => FromLambda(expr.Body);
        /// <summary> Gets the top most invoked method from the expression. For example `(String a) => a.Trim(anything)` will return descriptor of the Trim method. The function also supports properties (it will return the getter) and constuctors (using the `new XXX()` syntax). </summary>
        public static MethodReference FromLambda<T>(LE.Expression<Action<T>> expr) => FromLambda(expr.Body);
        /// <summary> Gets the top most invoked method from the expression. For example `(String a) => a.Trim(anything)` will return descriptor of the Trim method. The function also supports properties (it will return the getter) and constuctors (using the `new XXX()` syntax). </summary>
        public static MethodReference FromLambda(LE.Expression<Func<object>> expr) => FromLambda(expr.Body);
        /// <summary> Gets the top most invoked method from the expression. For example `(String a) => a.Trim(anything)` will return descriptor of the Trim method. The function also supports properties (it will return the getter) and constuctors (using the `new XXX()` syntax). </summary>
        public static MethodReference FromLambda(LE.Expression<Action> expr) => FromLambda(expr.Body);
        /// <summary> Gets the top most invoked method from the expression. For example `(String a) => a.Trim(anything)` will return descriptor of the Trim method. The function also supports properties (it will return the getter) and constuctors (using the `new XXX()` syntax). </summary>
        public static MethodReference FromLambda(LE.Expression expr)
        {
            var b = expr;
            while (b is LE.UnaryExpression uExpr && uExpr.NodeType == LE.ExpressionType.Convert)
                b = uExpr.Operand;

            switch (b)
            {
                case LE.MethodCallExpression methodCall:
                    return FromReflection(methodCall.Method);

                case LE.MemberExpression memberExpr:
                    if (memberExpr.Member is R.PropertyInfo prop)
                        return FromReflection(prop.GetMethod);
                    else
                        throw new NotSupportedException($"Can't get method reference from member {memberExpr.Member}");
                case LE.NewExpression newExpr:
                    return FromReflection(newExpr.Constructor);
                // this actually does not work:
                // case LE.NewArrayExpression newArrExpr: {
                //     var array = newArrExpr.Type;
                //     var dim = newArrExpr.Expressions.Count;
                //     Assert.Equal(array.GetArrayRank(), dim);
                //     var ctor = array.GetConstructors().Single(c => c.GetParameters().Length == dim);
                //     return FromReflection(ctor);
                // }
                default:
                    throw new NotSupportedException($"Can't get method reference from expression {b}");
            }
        }
    }
}
