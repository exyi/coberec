using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class ParameterExpression
    {
        /// <summary> Creates a new immutable <see cref="ParameterExpression" /> from the <paramref name="parameter" /> by copying its type and name. </summary>
        public static ParameterExpression Create(MethodParameter parameter) => Create(parameter.Type, parameter.Name);
        /// <summary> Creates a new immutable <see cref="ParameterExpression" />. </summary>
        public static ParameterExpression Create(TypeReference type, string name) => new ParameterExpression(Guid.NewGuid(), name, type, mutable: false);
        /// <summary> Creates a new mutable <see cref="ParameterExpression" />. </summary>
        public static ParameterExpression CreateMutable(TypeReference type, string name) => new ParameterExpression(Guid.NewGuid(), name, type, mutable: true);
        /// <summary> Creates a new immutable <see cref="ParameterExpression" /> with name `this` of the specified type. If the <paramref name="declaringType" /> is a value type, the parameter type will be a reference. </summary>
        public static ParameterExpression CreateThisParam(TypeSignature declaringType, string name = "this") =>
            declaringType.IsValueType ? Create(TypeReference.ByReferenceType(declaringType), name) :
                                        Create(declaringType, name);
        /// <summary> Creates a new immutable <see cref="ParameterExpression" /> with name `this` for the specified method. If the <paramref name="method"/>`.DeclaringType` is a value type, the parameter type will be a reference. </summary>
        public static ParameterExpression CreateThisParam(MethodSignature method, string name = "this") =>
            method.IsStatic ? throw new Exception($"Can't create parameter expression for method {method.DeclaringType.Name}.{method.Name} as it's static") :
            CreateThisParam(method.DeclaringType);


        /// <summary> Just returns expression that reads value of this parameter. </summary>
        public Expression Read() => Expression.Parameter(this);
        /// <summary> Creates an expression with a reference to this local variable. Note that assigning to the reference will only work if it the parameter is mutable. </summary>
        public Expression Ref() => Expression.VariableReference(this);
        /// <summary> Creates assignment expression from this parameter and the specified <paramref name="value" />. Note that it only works when the variable is mutable. </summary>
        public Expression Assign(Expression value)
        {
            if (!Mutable)
                throw new Exception($"Can not assign {value} to immutable variable {this}");
            return Expression.VariableReference(this).ReferenceAssign(value);
        }
    }
}
