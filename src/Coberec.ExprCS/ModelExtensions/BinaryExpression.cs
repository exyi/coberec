namespace Coberec.ExprCS
{
    public partial class BinaryExpression
    {
        public bool IsComparison() => this.Operator switch {
            "==" => true,
            "<=" => true,
            ">=" => true,
            "!=" => true,
            "<" => true,
            ">" => true,
            _ => false
        };
    }
}
