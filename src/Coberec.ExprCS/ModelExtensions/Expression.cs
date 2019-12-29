using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Represents a code fragment - a single expression. May have many forms, see the nested classes for more information. </summary>
    public partial class Expression
    {
        /// <summary> Does nothing and returns void. </summary>
        public static readonly Expression Nop = Expression.Default(TypeSignature.Void);
        /// <summary> Gets the result type of the expression. </summary>
        public TypeReference Type() =>
            this.Match<TypeReference>(
                e => e.Item.Left.Type(),
                e => e.Item.Expr.Type(),
                e => e.Item.Method.ResultType(),
                e => e.Item.Ctor.DeclaringType(),
                fieldAccess: e => TypeReference.ByReferenceType(e.Item.Field.ResultType()),
                referenceAssign: e => TypeSignature.Void,
                dereference: e => ((TypeReference.ByReferenceTypeCase)e.Item.Expr.Type()).Item.Type,
                variableReference: e => TypeReference.ByReferenceType(e.Item.Variable.Type),
                addressOf: e => TypeReference.ByReferenceType(e.Item.Expr.Type()),
                numericConversion: e => e.Item.Type,
                referenceConversion: e => e.Item.Type,
                constant: e => e.Item.Type,
                @default: e => e.Item.Type,
                parameter: e => e.Item.Type,
                conditional: e => e.Item.IfTrue.Type(),
                function: e => TypeReference.FunctionType(e.Item.Params, e.Item.Body.Type()),
                functionConversion: e => e.Item.Type,
                invoke: e => ExtractFunctionReturnType(e.Item.Function.Type()),
                e => TypeSignature.Void,
                e => e.Item.Expression.Type(),
                e => TypeSignature.Void,
                e => e.Item.Target.Type(),
                e => e.Item.Type,
                e => new ByReferenceType(((TypeReference.ArrayTypeCase)e.Item.Array.Type()).Item.Type),
                e => e.Item.Result.Type(),
                e => e.Item.Type);

        public bool CanTakeReference(bool mutable) =>
            this.Match<bool>(
                binary: _ => false,
                not: _ => false,
                methodCall: e => false,
                newObject: e => false,
                fieldAccess: e => !mutable || !e.Item.Field.Signature.IsReadonly,
                referenceAssign: e => false,
                dereference: e => e.Item.Expr.CanTakeReference(mutable),
                variableReference: e => false,
                addressOf: e => false,
                numericConversion: e => false,
                referenceConversion: e => false,
                constant: e => false,
                @default: e => false,
                parameter: e => !mutable || e.Item.Mutable,
                conditional: e => false,
                function: e => false,
                functionConversion: e => false,
                invoke: e => false,
                @break: e => false,
                breakable: e => false,
                loop: e => false,
                letIn: e => e.Item.Target.CanTakeReference(mutable),
                newArray: e => false,
                arrayIndex: e => true,
                block: e => e.Item.Result.CanTakeReference(mutable),
                lowerable: e => e.Item.Lowered.CanTakeReference(mutable));

        static TypeReference ExtractFunctionReturnType(TypeReference type) =>
            Assert.IsType<TypeReference.FunctionTypeCase>(type).Item.ResultType;

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
            return Expression.Conditional(a, b, Expression.Constant(false));
        }

        public static Expression AndAlso(params Expression[] clauses) => AndAlso(clauses.AsEnumerable());
        public static Expression AndAlso(IEnumerable<Expression> clauses) =>
            clauses.Any() ?
            clauses.Aggregate(AndAlso) :
            Expression.Constant(true);

        public static Expression StaticMethodCall(MethodReference method, params Expression[] args) =>
            StaticMethodCall(method, args.AsEnumerable());
        public static Expression StaticMethodCall(MethodReference method, IEnumerable<Expression> args)
        {
            if (!method.Signature.IsStatic)
                throw new ArgumentException($"Static method was expected, got {method}", nameof(method));
            Assert.Equal(method.Params().Select(p => p.Type), args.Select(a => a.Type()));
            return MethodCall(method, args.ToImmutableArray(), target: null);
        }

        public static Expression StaticFieldAccess(FieldReference field)
        {
            if (!field.Signature.IsStatic)
                throw new ArgumentException($"Static field was expected, got {field}", nameof(field));

            return Expression.FieldAccess(field, null);
        }
    }
}
