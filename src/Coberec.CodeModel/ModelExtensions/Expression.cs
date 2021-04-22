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
        /// <summary> Gets the result type of the expression. </summary>
        public TypeReference Type() =>
            this.Match<TypeReference>(
                binary: e => e.IsComparison() ? TypeSignature.Boolean : e.Left.Type(),
                e => e.Expr.Type(),
                e => e.Method.ResultType(),
                e => e.Ctor.DeclaringType(),
                fieldAccess: e => TypeReference.ByReferenceType(e.Field.ResultType()),
                referenceAssign: e => TypeSignature.Void,
                dereference: e => ((TypeReference.ByReferenceTypeCase)e.Expr.Type()).Item.Type,
                variableReference: e => TypeReference.ByReferenceType(e.Variable.Type),
                addressOf: e => TypeReference.ByReferenceType(e.Expr.Type()),
                numericConversion: e => e.Type,
                referenceConversion: e => e.Type,
                constant: e => e.Type,
                @default: e => e.Type,
                parameter: e => e.Type,
                conditional: e => e.IfTrue.Type(),
                function: e => TypeReference.FunctionType(e.Params, e.Body.Type()),
                functionConversion: e => e.Type,
                invoke: e => ExtractFunctionReturnType(e.Function.Type()),
                e => TypeSignature.Void,
                e => e.Expression.Type(),
                e => TypeSignature.Void,
                e => e.Target.Type(),
                e => e.Type,
                e => new ByReferenceType(((TypeReference.ArrayTypeCase)e.Array.Type()).Item.Type),
                e => e.Result.Type(),
                e => e.Type);

        public bool CanTakeReference(bool mutable) =>
            this.Match<bool>(
                binary: _ => false,
                not: _ => false,
                methodCall: e => false,
                newObject: e => false,
                fieldAccess: e => !mutable || !e.Field.Signature.IsReadonly,
                referenceAssign: e => false,
                dereference: e => e.Expr.CanTakeReference(mutable),
                variableReference: e => false,
                addressOf: e => false,
                numericConversion: e => false,
                referenceConversion: e => false,
                constant: e => false,
                @default: e => false,
                parameter: e => !mutable || e.Mutable,
                conditional: e => false,
                function: e => false,
                functionConversion: e => false,
                invoke: e => false,
                @break: e => false,
                breakable: e => false,
                loop: e => false,
                letIn: e => e.Target.CanTakeReference(mutable),
                newArray: e => false,
                arrayIndex: e => true,
                block: e => e.Result.CanTakeReference(mutable),
                lowerable: e => e.Lowered.CanTakeReference(mutable));

        static TypeReference ExtractFunctionReturnType(TypeReference type) =>
            Assert.IsType<TypeReference.FunctionTypeCase>(type).Item.ResultType;
    }
}
