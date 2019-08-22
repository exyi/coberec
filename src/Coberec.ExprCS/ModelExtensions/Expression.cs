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
        public TypeReference Type() =>
            this.Match<TypeReference>(
                e => e.Item.Left.Type(),
                e => e.Item.Expr.Type(),
                e => e.Item.Method.ResultType,
                e => e.Item.Ctor.DeclaringType,
                e => e.Item.Field.ResultType,
                e => TypeSignature.Void,
                e => e.Item.Type,
                e => e.Item.Type,
                e => e.Item.Type,
                e => e.Item.Type,
                e => e.Item.Type,
                e => e.Item.IfTrue.Type(),
                function: e => TypeReference.FunctionType(e.Item.Params, e.Item.Body.Type()),
                functionConversion: e => e.Item.Type,
                invoke: e => ExtractFunctionReturnType(e.Item.Function.Type()),
                e => TypeSignature.Void,
                e => e.Item.Expression.Type(),
                e => TypeSignature.Void,
                e => e.Item.Target.Type(),
                e => e.Item.Result.Type(),
                e => e.Item.Type);

        static TypeReference ExtractFunctionReturnType(TypeReference type) =>
            Assert.IsType<TypeReference.FunctionTypeCase>(type).Item.ResultType;

        public static Expression IfThen(Expression condition, Expression body)
        {
            if (body.Type() != TypeSignature.Void)
                throw new ValidationErrorException(ValidationErrors.Create("Block of a IfThen expression must be void.").Nest("ifTrue"));

            return Expression.Conditional(condition, body, Expression.Default(TypeSignature.Void));
        }

        public static Expression While(Expression condition, params Expression[] body)
        {
            var label = LabelTarget.New("cycleBreak");
            var bb = Expression.Breakable(
                Expression.Loop(
                    Expression.Block(
                        body.Prepend(
                            Expression.IfThen(Expression.Not(condition), Expression.Break(Expression.Default(TypeSignature.Void), label)))
                            .ToImmutableArray(),
                        Expression.Default(TypeSignature.Void)
                    )
                ),
                label
            );
            return bb;
        }
    }
}