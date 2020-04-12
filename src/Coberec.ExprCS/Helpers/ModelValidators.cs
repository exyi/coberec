using System;
using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    public static class ModelValidators
    {
        public static ValidationErrors TypeIsNotRef(TypeReference type)
        {
            if (type is TypeReference.ByReferenceTypeCase byRef)
                return ValidationErrors.Create($"By reference type {type} can not be used when it would be put on a heap.");
            // TODO: ref structs
            return null;
        }

        public static ValidationErrors IsReferenceType(Expression e)
        {
            var type = e.Type();
            if (!(type is TypeReference.ByReferenceTypeCase))
                return ValidationErrors.Create($"Expected expression of by-ref type, got type '{type}'");
            return null;
        }

        public static ValidationErrors IsWritableReferenceType(Expression e)
        {
            if (IsReferenceType(e) is ValidationErrors r) return r;

            var err = e switch {
                Expression.AddressOfCase a => ValidationErrors.Create($"AddresOf expression returns read-only reference").Nest("AddressOf"),
                Expression.ArrayIndexCase a => null,
                Expression.BlockCase b => IsWritableReferenceType(b.Item.Result).Nest("result").Nest("Block"),
                Expression.BreakableCase b => null, // we don't know in this case :/ ; TODO: analyze more in-depth?
                Expression.ConditionalCase c => ValidationErrors.Join(
                    IsWritableReferenceType(c.Item.IfTrue).Nest("ifTrue"),
                    IsWritableReferenceType(c.Item.IfFalse).Nest("ifFalse")
                ).Nest("Conditional"),
                Expression.FieldAccessCase f =>
                    null,
                    // uh oh, even readonly fields can be assigned from the constructor ;(
                    //f.Item.Field.Signature.IsReadonly ? ValidationErrors.CreateField(new [] { "FieldAccess", "field", "signature", "isReadonly" }, $"Field {f.Item.Field} is readonly") : null,
                Expression.InvokeCase _ => null, // we don't know if method return readonly ref...
                Expression.MethodCallCase _ => null, // we don't know if method return readonly ref...
                Expression.LetInCase l => IsWritableReferenceType(l.Item.Target).Nest("target").Nest("LetIn"),
                Expression.VariableReferenceCase v => !v.Item.Variable.Mutable ? ValidationErrors.CreateField(new [] { "VariableReference", "variable", "mutable" }, $"Variable {v.Item.Variable} is not mutable") : null,
                Expression.ParameterCase p => null, // can't track mutability of variables :/
                var x =>
#if DEBUG
                    throw new NotSupportedException($"Expression {x} was supposed not to return reference")
#else
                    null
#endif
            };
            return err;
        }
    }
}
