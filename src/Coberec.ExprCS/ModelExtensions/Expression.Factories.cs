using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using Xunit;

namespace Coberec.ExprCS
{
    public partial class Expression
    {
        /// <summary> Returns an anonymous function (a lambda) </summary>
        public static Expression Function(Expression body, params ParameterExpression[] args) => Function(body, args.AsEnumerable());
        /// <summary> Returns an anonymous function (a lambda) </summary>
        public static Expression Function(Expression body, IEnumerable<ParameterExpression> args) =>
            Function(args.Select(a => new MethodParameter(a.Type, a.Name)).ToImmutableArray(), // TODO: translate ref to ref
                     args.ToImmutableArray(),
                     body);

        /// <summary> Conditionally executes the <paramref name="body" />. <paramref name="condition" /> must return bool, <paramref name="body" /> must return void and the result always returns void. </summary>
        public static Expression IfThen(Expression condition, Expression body)
        {
            if (body.Type() != TypeSignature.Void)
                throw new ValidationErrorException(ValidationErrors.Create("Block of a IfThen expression must be void.").Nest("ifTrue"));

            return Expression.Conditional(condition, body, Expression.Nop);
        }

        /// <summary> Executes the <paramref name="body" /> until the <paramref name="condition" /> is true. <paramref name="condition" /> must return bool, <paramref name="body" /> must return void and the result always returns void. </summary>
        public static Expression While(Expression condition, params Expression[] body)
        {
            var label = LabelTarget.New("cycleBreak");
            var bb = Expression.Breakable(
                Expression.Loop(
                    Expression.Block(
                        body.Prepend(
                            Expression.IfThen(Expression.Not(condition), Expression.Break(Expression.Nop, label)))
                            .ToImmutableArray(),
                        Expression.Nop
                    )
                ),
                label
            );
            return bb;
        }


        public static Expression Constant<T>(T obj)
        {
            var type = TypeReference.FromType(typeof(T));

            return Constant(obj, type);
        }

        public static Expression NewArray(ArrayType type, params Expression[] dimensions)
        {
            return NewArray(type, dimensions.ToImmutableArray());
        }

        public static Expression ArrayIndex(Expression array, params Expression[] dimensions)
        {
            return ArrayIndex(array, dimensions.ToImmutableArray());
        }

        public static Expression AndAlso(Expression a, Expression b)
        {
            Assert.Equal(TypeSignature.Boolean, a.Type());
            Assert.Equal(TypeSignature.Boolean, b.Type());
            if (a is Expression.ConstantCase { Item: { Value: bool a_const } })
                return a_const ? b : Expression.Constant(false);
            else if (b is Expression.ConstantCase { Item: { Value: bool b_const } })
                return b_const ? a : Expression.Constant(false);
            else
                return Expression.Conditional(a, b, Expression.Constant(false));
        }

        public static Expression AndAlso(params Expression[] clauses) => AndAlso(clauses.AsEnumerable());
        public static Expression AndAlso(IEnumerable<Expression> clauses) =>
            clauses.Any() ?
            clauses.Aggregate(AndAlso) :
            Expression.Constant(true);

        /// <summary> Calls the specified static method. It will automatically fill in optional parameters </summary>
        public static Expression StaticMethodCall(MethodReference method, params Expression[] args) =>
            StaticMethodCall(method, args.AsEnumerable());
        /// <summary> Calls the specified static method. It will automatically fill in optional parameters </summary>
        public static Expression StaticMethodCall(MethodReference method, IEnumerable<Expression> args)
        {
            if (!method.Signature.IsStatic)
                throw new ArgumentException($"Static method was expected, got {method}", nameof(method));
            var pargs = FluentExpression.PrepareArguments(args.ToImmutableArray(), method);
            return MethodCall(method, pargs, target: null);
        }

        /// <summary> Creates new object using the specified constructor </summary>
        public static Expression NewObject(MethodReference constructor, params Expression[] args) =>
            NewObject(constructor, args.ToImmutableArray());

        public static Expression StaticFieldAccess(FieldReference field)
        {
            if (!field.Signature.IsStatic)
                throw new ArgumentException($"Static field was expected, got {field}", nameof(field));

            return Expression.FieldAccess(field, null);
        }

        /// <summary> Gets a value of the static <paramref name="field" />. If you want to get the reference, use <see cref="StaticFieldAccess(FieldReference)" /> </summary>
        public static Expression StaticFieldRead(FieldReference field) =>
            Expression.StaticFieldAccess(field).Dereference();
        /// <summary> Writes <paramref name="value" /> into the static <paramref name="property" />. </summary>
        public static Expression StaticFieldAssign(FieldReference field, Expression value) =>
            Expression.StaticFieldAccess(field).ReferenceAssign(value);


        /// <summary> Calls getter of the static <paramref name="property" />. </summary>
        public static Expression StaticPropertyRead(PropertyReference property)
        {
            if (!property.Signature.IsStatic)
                throw new ArgumentException($"Static property was expected, got {property}", nameof(property));
            var getter = property.Getter();
            if (getter is null) throw new ArgumentException($"Can not read property {property}", nameof(property));
            return Expression.MethodCall(getter, ImmutableArray<Expression>.Empty, null);
        }
        /// <summary> Writes <paramref name="value" /> into the static <paramref name="property" />. </summary>
        public static Expression StaticPropertyAssign(PropertyReference property, Expression value)
        {
            if (!property.Signature.IsStatic)
                throw new ArgumentException($"Static property was expected, got {property}", nameof(property));
            var setter = property.Setter();
            if (setter is null) throw new ArgumentException($"Can not write to property {property}", nameof(property));
            return Expression.MethodCall(setter, ImmutableArray.Create(value), null);
        }
    }
}
