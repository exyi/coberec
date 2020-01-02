using Coberec.CoreLib;

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

        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, BinaryExpression obj)
        {
            if (obj.Left.Type() != obj.Right.Type())
                e.Add(ValidationErrors.Create($"Binary expressions's left and right subexpression must have the same type. Left: '{obj.Left.Type()}' Right: '{obj.Right.Type()}'").Nest("left"));
        }
    }
}
