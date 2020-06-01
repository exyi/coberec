using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    partial class ConditionalExpression
    {
        static partial void ValidateObjectExtension(ref CoreLib.ValidationErrorsBuilder e, ConditionalExpression obj)
        {
            if (obj.Condition is object && obj.Condition.Type() != TypeSignature.Boolean)
                e.Add(ValidationErrors.Create($"condition can only handle expr of type bool, not '{obj.Condition.Type()}'").Nest("condition"));
            if (obj.IfFalse is object && obj.IfTrue is object && obj.IfFalse.Type() != obj.IfTrue.Type())
                e.Add(ValidationErrors.Create($"true and false branches must have the same type. true: '{obj.IfTrue.Type()}', false: '{obj.IfFalse.Type()}'").Nest("ifFalse"));
        }
    }
}
