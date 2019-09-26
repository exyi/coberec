using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using Coberec.CoreLib;
using Coberec.CSharpGen.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using static Coberec.CSharpGen.Emit.EmitExtensions;
using System.Reflection.Metadata;
using E=Coberec.ExprCS;

namespace Coberec.CSharpGen.Emit
{
    public static class MatchFunctionImplementation
    {
        public static IMethod ImplementMatchBase(this VirtualType type, (IType caseType, string caseName)[] cases)
        {
            var genericParameter = new DefaultTypeParameter(type, 0, "TResult");
            var func = type.Compilation.FindType(typeof(Func<,>));
            IType makeArgType(IType caseType) => new ParameterizedType(func, new [] { caseType, genericParameter });

            var parameters = E.SymbolNamer.NameParameters(cases.Select(c => new VirtualParameter(makeArgType(c.caseType), c.caseName)));
            var methodName = E.SymbolNamer.NameMethod(type, "Match", 1, parameters, false);
            var method = new VirtualMethod(type, Accessibility.Public, methodName, parameters, genericParameter, isVirtual: true, isAbstract: true, typeParameters: new [] { genericParameter });

            type.Methods.Add(method);
            return method;
        }

        public static IMethod ImplementMatchCase(this VirtualType type, IMethod baseMethod, int caseIndex)
        {
            var caseP = baseMethod.Parameters[caseIndex];

            var method = new VirtualMethod(type, Accessibility.Public, baseMethod.Name, baseMethod.Parameters, baseMethod.ReturnType, isOverride: true, typeParameters: baseMethod.TypeParameters.ToArray());

            method.BodyFactory = () => {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                var funcParam = new IL.ILVariable(IL.VariableKind.Parameter, caseP.Type, caseIndex);
                var invokeMethod = funcParam.Type.GetMethods(m => m.Parameters.Count == 1 && m.Name == "Invoke").Single();
                return CreateExpressionFunction(method,
                    new IL.CallVirt(invokeMethod) { Arguments = { new IL.LdLoc(funcParam), new IL.LdLoc(thisParam) } }
                );
            };

            type.Methods.Add(method);
            return method;
        }
    }
}
