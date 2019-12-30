using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using TS=ICSharpCode.Decompiler.TypeSystem;
using Coberec.CoreLib;
using Coberec.CSharpGen.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using E=Coberec.ExprCS;
using Coberec.ExprCS;

namespace Coberec.CSharpGen.Emit
{
    public static class MatchFunctionImplementation
    {
        static PropertySignature GetItemProperty(TypeDef caseType) =>
            caseType.Members.OfType<PropertyDef>().Single(m => m.Signature.Name == "Item").Signature;
        public static MethodDef ImplementMatchBase(TypeSignature declaringType, (TypeDef caseType, string caseName)[] cases)
        {
            var genericParameter = new GenericParameter(Guid.NewGuid(), "T");
            var func = TypeSignature.FromType(typeof(Func<,>));
            TypeReference makeArgType(TypeDef caseType) => func.Specialize(GetItemProperty(caseType).Type, genericParameter);

            var parameters = cases.Select(c => new MethodParameter(makeArgType(c.caseType), c.caseName));
            var method = new MethodSignature(declaringType, parameters.ToImmutableArray(), "Match", genericParameter, isStatic: false, Accessibility.APublic, isVirtual: true, isOverride: false, isAbstract: true, hasSpecialName: false, typeParameters: ImmutableArray.Create(genericParameter));

            return MethodDef.InterfaceDef(method);
        }

        public static MethodDef ImplementMatchCase(TypeDef caseType, MethodSignature baseMethod, int caseIndex)
        {
            var caseP = baseMethod.Params[caseIndex];

            var method = MethodSignature.Override(caseType.Signature, baseMethod);

            return MethodDef.CreateWithArray(method, args => {
                Expression @this = args[0];
                Expression myFunc = args[caseIndex + 1];
                var itemProperty = GetItemProperty(caseType);
                var function = Expression.FunctionConversion(myFunc, TypeReference.FunctionType(ImmutableArray.Create(new MethodParameter(itemProperty.Type, "arg")), method.TypeParameters.Single()));

                return function.Invoke(@this.ReadProperty(itemProperty));
            });
        }
    }
}
