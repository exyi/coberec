using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;
using Xunit;

namespace Coberec.ExprCS
{
    public static class FluentExpression
    {
        public static Expression Dereference(this Expression expr) => Expression.Dereference(expr);
        public static Expression ReferenceAssign(this Expression target, Expression value) => Expression.ReferenceAssign(target, value);

        /// <summary> <see cref="LetInExpression" />, just with the variable declaration after it is used (syntactically only, of course). Inspired by Haskell's `where` keyword. </summary>
        public static Expression Where(this Expression target, ParameterExpression variable, Expression value) =>
            Expression.LetIn(variable, value, target);

        /// <summary> Collects all expression in the collection and puts them into a block expression. The <paramref name="result" /> specifies the result value returned after all the previous expressions are evaluated. </summary>
        public static Expression ToBlock(this IEnumerable<Expression> expressions, Expression result = null) =>
            Expression.Block(expressions.ToImmutableArray(), result ?? Expression.Nop);

        /// <summary> Calls the specified instance method on the <paramref name="target" />. Can be also used to call extension methods </summary>
        public static Expression CallMethod(this Expression target, MethodReference method, IEnumerable<Expression> args) =>
            CallMethod(target, method, args.ToArray());

        /// <summary> Calls the specified instance method on the <paramref name="target" />. Can be also used to call extension methods and it will automatically fill in optional parameters </summary>
        public static Expression CallMethod(this Expression target, MethodReference method, params Expression[] args)
        {
            if (method.Signature.IsStatic)
                // probably extension method
                return Expression.StaticMethodCall(method, args.Prepend(target));
            else
            {
                var pargs = PrepareArguments(args.ToImmutableArray(), method);
                return Expression.MethodCall(method, pargs, target);
            }
        }

        internal static ImmutableArray<Expression> PrepareArguments(ImmutableArray<Expression> args, MethodReference method)
        {
            if (method.Signature.Params.Length <= args.Length) return args;
            //                                  ^ if it's out of range, just let the validation fail
            // add the optional arguments
            var optParams =
                method.Params()
                .EagerSlice(skip: args.Length)
                .EagerSelect(p => p.HasDefaultValue ? Expression.Constant(p.DefaultValue, p.Type) : null);
            var valid = optParams.All(x => x is object);
            if (valid) return args.AddRange(optParams);
            else return args; // just let the validation fail
        }

        /// <summary> Gets a reference pointing to the instance <paramref name="field" /> on the <paramref name="target" /> </summary>
        public static Expression AccessField(this Expression target, FieldReference field) =>
            Expression.FieldAccess(field, target);

        /// <summary> Gets a value of the instance <paramref name="field" /> on the <paramref name="target" />. If you want to get the reference, use <see cref="AccessField(Expression, FieldReference)" /> </summary>
        public static Expression ReadField(this Expression target, FieldReference field) =>
            Expression.FieldAccess(field, target).Dereference();
        /// <summary> Writes <paramref name="value" /> into the <paramref name="field" /> on the <paramref name="target" />. </summary>
        public static Expression AssignField(this Expression target, FieldReference field, Expression value) =>
            Expression.FieldAccess(field, target).ReferenceAssign(value);

        /// <summary> Calls getter of the static <paramref name="property" />. </summary>
        public static Expression ReadProperty(this Expression target, PropertyReference property)
        {
            if (property.Signature.IsStatic)
                throw new ArgumentException($"Instance property was expected, got {property}", nameof(property));
            var getter = property.Getter();
            if (getter is null) throw new ArgumentException($"Can not read property {property}", nameof(property));
            return Expression.MethodCall(getter, ImmutableArray<Expression>.Empty, target);
        }
        /// <summary> Writes <paramref name="value" /> into the static <paramref name="property" />. </summary>
        public static Expression AssignProperty(this Expression target, PropertyReference property, Expression value)
        {
            if (property.Signature.IsStatic)
                throw new ArgumentException($"Instance property was expected, got {property}", nameof(property));
            var setter = property.Setter();
            if (setter is null) throw new ArgumentException($"Can not write to property {property}", nameof(property));
            return Expression.MethodCall(setter, ImmutableArray.Create(value), target);
        }

        /// <summary> Creates a reference conversion of <paramref name="value" /> to <paramref name="type" /> </summary>
        public static Expression ReferenceConvert(this Expression value, TypeReference type) =>
            Expression.ReferenceConversion(value, type);

        /// <summary> Creates a function conversion of <paramref name="value" /> to <paramref name="type" /> </summary>
        public static Expression FunctionConvert(this Expression value, TypeReference type) =>
            Expression.FunctionConversion(value, type);

        /// <summary> Creates a reference conversion of <paramref name="value" /> to <see cref="System.Object" /> </summary>
        public static Expression Box(this Expression value) =>
            value.Type() == TypeSignature.Object ? value :
            Expression.ReferenceConversion(value, TypeSignature.Object);

        /// <summary> Creates a function invocation expression - invokes the <paramref name="function" /> with the specified <paramref name="args" /> </summary>
        public static Expression Invoke(this Expression function, params Expression[] args) => Invoke(function, args.ToImmutableArray());
        /// <summary> Creates a function invocation expression - invokes the <paramref name="function" /> with the specified <paramref name="args" /> </summary>
        public static Expression Invoke(this Expression function, IEnumerable<Expression> args) => Invoke(function, args.ToImmutableArray());
        /// <summary> Creates a function invocation expression - invokes the <paramref name="function" /> with the specified <paramref name="args" /> </summary>
        public static Expression Invoke(this Expression function, ImmutableArray<Expression> args)
        {
            return Expression.Invoke(function, args);
        }

        /// <summary> Creates expression `!expr`. If expression is constant, it is folded immediately. </summary>
        public static Expression Not(this Expression expr) =>
            expr is Expression.ConstantCase { Item: { Value: var constant } } ? Expression.Constant(!(bool)constant) :
            expr is Expression.NotCase { Item: { Expr: var innerExpr } } ? innerExpr :
            Expression.Not(expr);

        /// <summary> Creates a lambda function. This expression will be a body. </summary>
        public static Expression AsFunction(this Expression functionBody, params ParameterExpression[] parameters) => functionBody.AsFunction(parameters.ToImmutableArray());
        /// <summary> Creates a lambda function. This expression will be a body. </summary>
        public static Expression AsFunction(this Expression functionBody, IEnumerable<ParameterExpression> parameters) => functionBody.AsFunction(parameters.ToImmutableArray());
        /// <summary> Creates a lambda function. This expression will be a body. </summary>
        public static Expression AsFunction(this Expression functionBody, ImmutableArray<ParameterExpression> parameters)
        {
            return Expression.Function(parameters.EagerSelect(p => new MethodParameter(p.Type, p.Name)), parameters, functionBody);
        }

        /// <summary> Returns expression `<paramref name="expr" /> is null`. Works for reference types and for nullable value types (<see cref="System.Nullable{T}" />). When the expr is different value type, constant `false` is returned. </summary>
        public static Expression IsNull(this Expression expr)
        {
            var type = expr.Type();
            if (type.IsGenericInstanceOf(TypeSignature.NullableOfT))
                return ExpressionFactory.Nullable_HasValue(expr).Not();
            else return type.IsReferenceType switch {
                true => Expression.Binary("==", expr, Expression.Constant(null, type)),
                false => Expression.Constant(false),
                null => throw new Exception($"Can not check type {type} for nulls.")
            };
        }

        public static Expression NullCoalesce(this Expression a, Expression b)
        {
            var aAlias = ParameterExpression.Create(a.Type(), "tmp");
            return Expression.Conditional(aAlias.Read().IsNull().Not(), aAlias, b)
                   .Where(aAlias, a);
        }
    }
}
