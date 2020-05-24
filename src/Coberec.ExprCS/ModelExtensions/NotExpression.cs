using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    /// <summary> Expression that negates a given boolean expression. Equivalent of `!Expr` in C#. </summary>
    partial class NotExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, NotExpression obj)
        {
            if (obj.Expr != null && obj.Expr.Type() != TypeSignature.Boolean)
                e.Add(ValidationErrors.Create($"Not expression can only handle expr of type bool, not '{obj.Expr.Type()}'").Nest("expr")); // TODO: expression type validation
        }
    }
}
