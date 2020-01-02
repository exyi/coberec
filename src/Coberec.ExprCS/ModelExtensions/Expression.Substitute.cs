namespace Coberec.ExprCS
{
    public partial class Expression
    {
        // public Expression Substitute(ParameterExpression p, Expression ne) =>
        //     this.Match<Expression>(
        //         binary: e => e.With(left: e.Left.Substitute(p, ne), right: e.Right.Substitute(p, ne)),
        //         not: _ => false,
        //         methodCall: e => false,
        //         newObject: e => false,
        //         fieldAccess: e => !mutable || !e.Field.Signature.IsReadonly,
        //         referenceAssign: e => false,
        //         dereference: e => e.Expr.CanTakeReference(mutable),
        //         variableReference: e => false,
        //         addressOf: e => false,
        //         numericConversion: e => false,
        //         referenceConversion: e => false,
        //         constant: e => false,
        //         @default: e => false,
        //         parameter: e => !mutable || e.Mutable,
        //         conditional: e => false,
        //         function: e => false,
        //         functionConversion: e => false,
        //         invoke: e => false,
        //         @break: e => false,
        //         breakable: e => false,
        //         loop: e => false,
        //         letIn: e => e.Target.CanTakeReference(mutable),
        //         newArray: e => false,
        //         arrayIndex: e => true,
        //         block: e => e.Result.CanTakeReference(mutable),
        //         lowerable: e => e.Lowered.CanTakeReference(mutable));
    }
}
