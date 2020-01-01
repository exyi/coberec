namespace Coberec.ExprCS
{
    public partial class Expression
    {
        // public Expression Substitute(ParameterExpression p, Expression ne) =>
        //     this.Match<Expression>(
        //         binary: e => e.Item.With(left: e.Item.Left.Substitute(p, ne), right: e.Item.Right.Substitute(p, ne)),
        //         not: _ => false,
        //         methodCall: e => false,
        //         newObject: e => false,
        //         fieldAccess: e => !mutable || !e.Item.Field.Signature.IsReadonly,
        //         referenceAssign: e => false,
        //         dereference: e => e.Item.Expr.CanTakeReference(mutable),
        //         variableReference: e => false,
        //         addressOf: e => false,
        //         numericConversion: e => false,
        //         referenceConversion: e => false,
        //         constant: e => false,
        //         @default: e => false,
        //         parameter: e => !mutable || e.Item.Mutable,
        //         conditional: e => false,
        //         function: e => false,
        //         functionConversion: e => false,
        //         invoke: e => false,
        //         @break: e => false,
        //         breakable: e => false,
        //         loop: e => false,
        //         letIn: e => e.Item.Target.CanTakeReference(mutable),
        //         newArray: e => false,
        //         arrayIndex: e => true,
        //         block: e => e.Item.Result.CanTakeReference(mutable),
        //         lowerable: e => e.Item.Lowered.CanTakeReference(mutable));
    }
}
