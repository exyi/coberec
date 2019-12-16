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
        /// <summary> Does nothing and returns void. </summary>
        public static readonly Expression Nop = Expression.Default(TypeSignature.Void);
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
                e => e.Item.Result.Type(),
                e => e.Item.Type);

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

    }
}
