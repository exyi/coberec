using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using TrainedMonkey.CoreLib;
using TrainedMonkey.CSharpGen.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;

namespace TrainedMonkey.CSharpGen.Emit
{
    public static class WithMethodImplementation
    {
        public static IMethod ImplementWithMethod(this VirtualType type, IMethod createConstructor, params IMember[] properties)
        {
            var ctorParameters = createConstructor.Parameters;
            Debug.Assert(properties.Zip(ctorParameters, (prop, param) => prop.ReturnType.Equals(param.Type)).All(a=>a));
            Debug.Assert(createConstructor.IsConstructor);

            var parameters = SymbolNamer.NameParameters(properties.Zip(ctorParameters, (prop, param) => new DefaultParameter(prop.ReturnType, param.Name)));
            var withName = SymbolNamer.NameMethod(type, "With", 0, parameters.Select(p => p.Type).ToArray());


            var method = new VirtualMethod(type, Accessibility.Public, withName, parameters, type);

            method.BodyFactory = () => {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                var constructorCall = new IL.NewObj(createConstructor);
                constructorCall.Arguments.AddRange(properties.Select(p => new IL.LdLoc(thisParam).AccessMember(p)));
                return EmitExtensions.CreateExpressionFunction(method,
                    new IL.IfInstruction(
                        EmitExtensions.AndAlso(
                        properties
                        .Select((mem, parIndex) => EmitExtensions.EqualsExpression(mem.ReturnType, new IL.LdLoc(thisParam).AccessMember(mem), new IL.LdLoc(new IL.ILVariable(IL.VariableKind.Parameter, mem.ReturnType, parIndex))))),
                    new IL.LdLoc(thisParam),
                    constructorCall
                ));
            };

            type.Methods.Add(method);

            return method;
        }

        public static IMethod ImplementOptionalWithMethod(this VirtualType type, IMethod withMethod, IMember[] properties)
        {
            IType optParamType(IType t) => new ParameterizedType(type.Compilation.FindType(typeof(OptParam<>)), new [] { t });

            var parameters = SymbolNamer.NameParameters(properties.Select(p => new DefaultParameter(optParamType(p.ReturnType), name: p.Name, isOptional: true)));

            var name = SymbolNamer.NameMethod(type, "With", 0, parameters.Select(p => p.Type).ToArray());
            var method = new VirtualMethod(type, Accessibility.Public, name, parameters, type);

            method.BodyFactory = () => {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                var normalWithCall = new IL.CallVirt(withMethod);
                var paramsWithVariables =
                    parameters.Select((p, index) =>
                        new { p, index, var = new IL.ILVariable(IL.VariableKind.Parameter, p.Type, index) });
                normalWithCall.Arguments.Add(new IL.LdLoc(thisParam));
                normalWithCall.Arguments.AddRange(properties.Zip(paramsWithVariables, (p, parameter) =>
                    new IL.Call(parameter.p.Type.GetMethods(m => m.Name == nameof(OptParam<int>.ValueOrDefault)).Single()) { Arguments = { new IL.LdLoca(parameter.var), new IL.LdLoc(thisParam).AccessMember(p) } }
                ));
                return EmitExtensions.CreateExpressionFunction(method,
                    normalWithCall
                );
            };

            type.Methods.Add(method);
            return method;
        }
    }
}