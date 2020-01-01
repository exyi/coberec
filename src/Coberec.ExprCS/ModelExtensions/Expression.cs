using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Represents a code fragment - a single expression. May have many forms, see the nested classes for more information. </summary>
    public partial class Expression
    {
        /// <summary> Does nothing and returns void. </summary>
        public static readonly Expression Nop = Expression.Default(TypeSignature.Void);
        /// <summary> Gets the result type of the expression. </summary>
        public TypeReference Type() =>
            this.Match<TypeReference>(
                binary: e => e.Item.IsComparison() ? TypeSignature.Boolean : e.Item.Left.Type(),
                e => e.Item.Expr.Type(),
                e => e.Item.Method.ResultType(),
                e => e.Item.Ctor.DeclaringType(),
                fieldAccess: e => TypeReference.ByReferenceType(e.Item.Field.ResultType()),
                referenceAssign: e => TypeSignature.Void,
                dereference: e => ((TypeReference.ByReferenceTypeCase)e.Item.Expr.Type()).Item.Type,
                variableReference: e => TypeReference.ByReferenceType(e.Item.Variable.Type),
                addressOf: e => TypeReference.ByReferenceType(e.Item.Expr.Type()),
                numericConversion: e => e.Item.Type,
                referenceConversion: e => e.Item.Type,
                constant: e => e.Item.Type,
                @default: e => e.Item.Type,
                parameter: e => e.Item.Type,
                conditional: e => e.Item.IfTrue.Type(),
                function: e => TypeReference.FunctionType(e.Item.Params, e.Item.Body.Type()),
                functionConversion: e => e.Item.Type,
                invoke: e => ExtractFunctionReturnType(e.Item.Function.Type()),
                e => TypeSignature.Void,
                e => e.Item.Expression.Type(),
                e => TypeSignature.Void,
                e => e.Item.Target.Type(),
                e => e.Item.Type,
                e => new ByReferenceType(((TypeReference.ArrayTypeCase)e.Item.Array.Type()).Item.Type),
                e => e.Item.Result.Type(),
                e => e.Item.Type);

        public bool CanTakeReference(bool mutable) =>
            this.Match<bool>(
                binary: _ => false,
                not: _ => false,
                methodCall: e => false,
                newObject: e => false,
                fieldAccess: e => !mutable || !e.Item.Field.Signature.IsReadonly,
                referenceAssign: e => false,
                dereference: e => e.Item.Expr.CanTakeReference(mutable),
                variableReference: e => false,
                addressOf: e => false,
                numericConversion: e => false,
                referenceConversion: e => false,
                constant: e => false,
                @default: e => false,
                parameter: e => !mutable || e.Item.Mutable,
                conditional: e => false,
                function: e => false,
                functionConversion: e => false,
                invoke: e => false,
                @break: e => false,
                breakable: e => false,
                loop: e => false,
                letIn: e => e.Item.Target.CanTakeReference(mutable),
                newArray: e => false,
                arrayIndex: e => true,
                block: e => e.Item.Result.CanTakeReference(mutable),
                lowerable: e => e.Item.Lowered.CanTakeReference(mutable));

        static TypeReference ExtractFunctionReturnType(TypeReference type) =>
            Assert.IsType<TypeReference.FunctionTypeCase>(type).Item.ResultType;
    }
}
