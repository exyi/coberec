using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class ParameterExpression
    {
        public static ParameterExpression Create(MethodParameter parameter) => Create(parameter.Type, parameter.Name);
        public static ParameterExpression Create(TypeReference type, string name) => new ParameterExpression(Guid.NewGuid(), name, type, mutable: false);
        public static ParameterExpression CreateMutable(TypeReference type, string name) => new ParameterExpression(Guid.NewGuid(), name, type, mutable: true);
        public static ParameterExpression CreateThisParam(TypeSignature declaringType, string name = "this") =>
            declaringType.IsValueType ? Create(TypeReference.ByReferenceType(declaringType), name) :
                                        Create(declaringType, name);
        public static ParameterExpression CreateThisParam(MethodSignature method, string name = "this") =>
            method.IsStatic ? throw new Exception($"Can't create parameter expression for method {method.DeclaringType.Name}.{method.Name} as it's static") :
            CreateThisParam(method.DeclaringType);
    }
}
