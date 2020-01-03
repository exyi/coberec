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
            if (e.HasErrors) return;

            var type = obj.Left.Type();
            if (type != obj.Right.Type())
                e.Add(ValidationErrors.Create($"Binary expressions's left and right subexpression must have the same type. Left: '{obj.Left.Type()}' Right: '{obj.Right.Type()}'").Nest("left"));

            // TODO: go through all operators and check that

            if (type is TypeReference.SpecializedTypeCase st && (st.Item.Type.IsPrimitive() || st.Item.Type.Kind == "enum"))
                return; // all operators are allowed on primitive types

            if (obj.IsComparison())
            {
                e.Add(type.Match(
                    st => st.Type.IsValueType ? ValidationErrors.Create($"Can not compare (operator {obj.Operator}) non-primitive value type {st}.") : null, // non-primitive value types can not be compared
                    a => null,
                    bref => ValidationErrors.Create($"Can not apply operator ({obj.Operator}) to by ref type {bref}."),
                    ptr => null,
                    g => null,
                    f => null
                ));
                if (obj.Operator != "==" && obj.Operator != "!=" && type.IsReferenceType != false)
                    e.Add(ValidationErrors.Create($"Can not apply operator ({obj.Operator}) to type {type} as it's a reference type."));
            }
            else
            {
                e.Add(ValidationErrors.Create($"Operator ({obj.Operator}) can be only applied to primitive numeric types, not '{type}'"));
            }
        }
    }
}
