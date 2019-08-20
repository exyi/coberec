using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    public partial class Expression
    {
        public TypeReference Type() =>
            this.Match<TypeReference>(
                e => e.Item.Left.Type(),
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
                e => e.Item.ResultType,
                e => throw new NotImplementedException(), // TODO:
                e => TypeSignature.Void,
                e => e.Item.Expression.Type(),
                e => TypeSignature.Void,
                e => e.Item.Target.Type(),
                e => e.Item.Result.Type(),
                e => e.Item.Type);

        public static Expression IfThen(Expression condition, Expression block)
        {
            if (block.Type() != TypeSignature.Void)
                throw new ValidationErrorException(ValidationErrors.Create("Block of a IfThen expression must be void.").Nest("ifTrue"));

            return Expression.Conditional(condition, block, Expression.Default(TypeSignature.Void));
        }
    }
}
