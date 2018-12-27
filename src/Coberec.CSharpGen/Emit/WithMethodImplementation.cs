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

namespace Coberec.CSharpGen.Emit
{
    public static class WithMethodImplementation
    {
        static VirtualMethod CreateSignatureCore(VirtualType type, IEnumerable<(IType type, string desiredName)> properties, bool isOptional = false)
        {
            IType optParamType(IType t) => isOptional ? new ParameterizedType(type.Compilation.FindType(typeof(OptParam<>)), new [] { t }) : t;

            var parameters = SymbolNamer.NameParameters(properties.Select((p) => new DefaultParameter(optParamType(p.type), p.desiredName, isOptional: isOptional)));
            var withName = SymbolNamer.NameMethod(type, "With", 0, parameters.Select(p => p.Type).ToArray());

            return new VirtualMethod(type, Accessibility.Public, withName, parameters, type);
        }

        public static IMethod InterfaceWithMethod(this VirtualType type, (IMember property, string desiredName)[] properties, bool isOptional)
        {
            Debug.Assert(type.Kind == TypeKind.Interface);

            var method = CreateSignatureCore(type, properties.Select((p) => (p.property.ReturnType, p.desiredName)), isOptional);
            type.Methods.Add(method);
            return method;
        }

        static IType UnwrapOptParam(IType t) =>
            t.Name == "OptParam" && t.Namespace == "Coberec.CoreLib" ? t.TypeArguments.Single() : t;
        public static IMember InterfaceImplementationWithMethod(this VirtualType type, IMethod localWithMethod, IMethod ifcMethod, (IMember localProperty, string desiredName)[] ifcProperties, IMember[] localProperties)
        {
            Debug.Assert(ifcProperties.Select(p => p.localProperty.ReturnType).SequenceEqual(ifcMethod.Parameters.Select(p => UnwrapOptParam(p.Type))));
            Debug.Assert(type.ImplementedInterfaces.Contains(ifcMethod.DeclaringType));
            Debug.Assert(type.ImplementedInterfaces.Contains(ifcMethod.ReturnType));

            Debug.Assert(localProperties.Select(p => p.ReturnType).SequenceEqual(localWithMethod.Parameters.Select(p => p.Type)));

            var method = new VirtualMethod(type, Accessibility.Private, ifcMethod.FullName, ifcMethod.Parameters, ifcMethod.ReturnType, explicitImplementations: new [] { ifcMethod });

            method.BodyFactory = () => {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);

                var expressions =
                   (from p in localProperties
                    let i = Array.FindIndex(ifcProperties, a => a.localProperty.Equals(p))
                    let parameter = i >= 0 ? ifcMethod.Parameters[i] : null
                    let parameterLocal = i >= 0 ? new IL.ILVariable(IL.VariableKind.Parameter, parameter.Type, i) : null
                    let isOptional = parameter?.Type.FullName == "Coberec.CoreLib.OptParam"
                    select i < 0 ? new IL.LdLoc(thisParam).AccessMember(p) :
                           isOptional ? OptParam_ValueOrDefault(parameter.Type, parameterLocal, new IL.LdLoc(thisParam).AccessMember(p)) :
                           new IL.LdLoc(parameterLocal)
                   ).ToArray();

                var withInvocation = new IL.Call(localWithMethod);
                withInvocation.Arguments.Add(new IL.LdLoc(thisParam));
                withInvocation.Arguments.AddRange(expressions);

                return EmitExtensions.CreateExpressionFunction(
                    method,
                    withInvocation
                );
            };

            type.Methods.Add(method);
            return method;
        }

        public static IMethod ImplementWithMethod(this VirtualType type, IMethod createConstructor, IMember[] properties)
        {
            var ctorParameters = createConstructor.Parameters;
            Debug.Assert(properties.Zip(ctorParameters, (prop, param) => prop.ReturnType.Equals(param.Type)).All(a=>a));
            Debug.Assert(createConstructor.IsConstructor);

            var method = CreateSignatureCore(type, properties.Zip(ctorParameters, (prop, param) => (prop.ReturnType, param.Name)));

            method.BodyFactory = () => {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                var constructorCall = new IL.NewObj(createConstructor);
                constructorCall.Arguments.AddRange(properties.Select(p => new IL.LdLoc(thisParam).AccessMember(p)));
                return EmitExtensions.CreateExpressionFunction(method,
                    new IL.IfInstruction(
                        EmitExtensions.AndAlso(
                            properties
                            .Select((mem, parIndex) => EqualityImplementation.EqualsExpression(mem.ReturnType, new IL.LdLoc(thisParam).AccessMember(mem), new IL.LdLoc(new IL.ILVariable(IL.VariableKind.Parameter, mem.ReturnType, parIndex))))),
                    new IL.LdLoc(thisParam),
                    constructorCall
                ));
            };

            type.Methods.Add(method);

            return method;
        }

        public static IMethod ImplementOptionalWithMethod(this VirtualType type, IMethod withMethod, IMember[] properties)
        {
            var method = CreateSignatureCore(type, withMethod.Parameters.Select(p => (p.Type, p.Name)), isOptional: true);

            method.BodyFactory = () => {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                var normalWithCall = new IL.CallVirt(withMethod);
                var paramsWithVariables =
                    method.Parameters.Select((p, index) =>
                        new { p, index, var = new IL.ILVariable(IL.VariableKind.Parameter, p.Type, index) });
                normalWithCall.Arguments.Add(new IL.LdLoc(thisParam));
                normalWithCall.Arguments.AddRange(properties.Zip(paramsWithVariables, (p, parameter) =>
                    OptParam_ValueOrDefault(parameter.p.Type, parameter.var, new IL.LdLoc(thisParam).AccessMember(p))
                ));
                return EmitExtensions.CreateExpressionFunction(method,
                    normalWithCall
                );
            };

            type.Methods.Add(method);
            return method;
        }

        static IL.ILInstruction OptParam_ValueOrDefault(IType parameterType, IL.ILVariable parameter, IL.ILInstruction defaultValue) =>
            new IL.Call(parameterType.GetMethods(m => m.Name == nameof(OptParam<int>.ValueOrDefault)).Single()) { Arguments = { new IL.LdLoca(parameter), defaultValue } };
    }
}