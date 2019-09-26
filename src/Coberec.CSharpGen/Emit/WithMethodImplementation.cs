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
using E=Coberec.ExprCS;

namespace Coberec.CSharpGen.Emit
{
    public static class WithMethodImplementation
    {
        static VirtualMethod CreateSignatureCore(VirtualType type, IEnumerable<(IType type, string desiredName)> properties, bool isOptional, bool returnValidationResult)
        {
            IType optParamType(IType t) => isOptional ? new ParameterizedType(type.Compilation.FindType(typeof(OptParam<>)), new [] { t }) : t;

            var parameters = E.SymbolNamer.NameParameters(properties.Select((p) => new VirtualParameter(optParamType(p.type), p.desiredName, isOptional: isOptional)));
            var withName = E.SymbolNamer.NameMethod(type, "With", 0, parameters, false);
            var returnType = returnValidationResult ?
                             new ParameterizedType(type.Compilation.FindType(typeof(ValidationResult<>)), new [] { type }) :
                             (IType)type;

            return new VirtualMethod(type, Accessibility.Public, withName, parameters, returnType);
        }

        public static E.MethodDef InterfaceWithMethod(E.TypeSignature type, (E.TypeReference type, string desiredName)[] properties, bool isOptional, bool returnValidationResult = true)
        {
            Debug.Assert(type.Kind == "interface");

            E.TypeReference optParamType(E.TypeReference t) => isOptional ? new E.SpecializedType(E.TypeSignature.FromType(typeof(OptParam<>)), new [] { t }) : t;
            var returnType = returnValidationResult ?
                             new E.SpecializedType(E.TypeSignature.FromType(typeof(ValidationResult<>)), new [] { (E.TypeReference)new E.SpecializedType(type) }) :
                             new E.SpecializedType(type);

            var method = E.MethodSignature.Instance("With", type, E.Accessibility.APublic, returnType, properties.Select((p) => new E.MethodParameter(optParamType(p.type), p.desiredName, isOptional, null)).ToArray());
            return new E.MethodDef(method, null, null);
        }

        static IType UnwrapOptParam(IType t) =>
            t.Name == "OptParam" && t.Namespace == "Coberec.CoreLib" ? t.TypeArguments.Single() : t;

        public static IMember InterfaceImplementationWithMethod(this VirtualType type, IMethod localWithMethod, IMethod ifcMethod, (IMember localProperty, string desiredName)[] ifcProperties, IMember[] localProperties)
        {
            Debug.Assert(ifcProperties.Select(p => p.localProperty.ReturnType).SequenceEqual(ifcMethod.Parameters.Select(p => UnwrapOptParam(p.Type))));
            Debug.Assert(type.ImplementedInterfaces.Contains(ifcMethod.DeclaringType));

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

                // invoke normal With method
                var withInvocation = new IL.Call(localWithMethod);
                withInvocation.Arguments.Add(new IL.LdLoc(thisParam));
                withInvocation.Arguments.AddRange(expressions);

                var castInvocation =
                    localWithMethod.ReturnType.GetAllBaseTypes().Contains(ifcMethod.ReturnType) ?
                        (IL.ILInstruction)withInvocation :
                    localWithMethod.ReturnType.FullName == typeof(ValidationResult).FullName && ifcMethod.ReturnType.FullName == typeof(ValidationResult).FullName ?
                        // cast ValidationResult<Type> into ValidationResult<Interface>
                        new IL.Call(localWithMethod.ReturnType.GetMethods(m => m.Name == "Cast" && m.TypeParameters.Count == 1 && m.Parameters.Count == 0).Single().Specialize(new TypeParameterSubstitution(null, new [] { ifcMethod.DeclaringType }))) {
                            Arguments = { new IL.AddressOf(withInvocation, localWithMethod.ReturnType) }
                        } :
                    localWithMethod.ReturnType.Equals(type) && ifcMethod.ReturnType.FullName == typeof(ValidationResult).FullName ?
                        new IL.Call(type.Compilation.FindType(typeof(ValidationResult)).GetMethods(m => m.Name == "Create" && m.Parameters.Count == 1).Single().Specialize(new TypeParameterSubstitution(null, new [] { ifcMethod.DeclaringType }))) {
                        // cast Type into ValidationResult<Interface>
                            Arguments = { withInvocation }
                        } :
                    localWithMethod.ReturnType.FullName == typeof(ValidationResult).FullName && ifcMethod.ReturnType.Equals(ifcMethod.DeclaringType) ?
                        // cast ValidationResult<Type> into Interface
                        (IL.ILInstruction)new IL.Call(localWithMethod.ReturnType.GetMethods(m => m.Name == nameof(ValidationResult<int>.Expect) && m.Parameters.Count == 1).Single()) {
                            Arguments = { new IL.AddressOf(withInvocation, localWithMethod.ReturnType), new IL.LdStr($"we can modify {type.Name}") }
                        } :
                    throw new NotSupportedException();

                return EmitExtensions.CreateExpressionFunction(
                    method,
                    castInvocation
                );
            };

            type.Methods.Add(method);
            return method;
        }

        public static IMethod ImplementWithMethod(this VirtualType type, IMethod factory, IMember[] properties, bool returnValidationResult = true)
        {
            var ctorParameters = factory.Parameters;
            Debug.Assert(properties.Zip(ctorParameters, (prop, param) => prop.ReturnType.Equals(param.Type)).All(a=>a));

            var method = CreateSignatureCore(type, properties.Zip(ctorParameters, (prop, param) => (prop.ReturnType, param.Name)), isOptional: false, returnValidationResult: returnValidationResult);

            Debug.Assert(factory.IsConstructor || factory.ReturnType.Equals(method.ReturnType));

            method.BodyFactory = () => {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                var parameters = properties.Select((p, i) => new IL.ILVariable(IL.VariableKind.Parameter, p.ReturnType, i)).ToArray();
                var factoryCall = factory.IsConstructor ? new IL.NewObj(factory) : (IL.CallInstruction)new IL.Call(factory);
                factoryCall.Arguments.AddRange(parameters.Select(v => new IL.LdLoc(v)));
                var noChangeExpression = returnValidationResult ?
                               new IL.Call(type.Compilation.FindType(typeof(ValidationResult)).GetMethods(m => m.Name == "Create" && m.Parameters.Count == 1).Single().Specialize(new TypeParameterSubstitution(null, new [] { type }))) {
                                   // cast Type into ValidationResult<Interface>
                                   Arguments = { new IL.LdLoc(thisParam) }
                               } :
                               (IL.ILInstruction)new IL.LdLoc(thisParam);
                return EmitExtensions.CreateExpressionFunction(method,
                    new IL.IfInstruction(
                        EmitExtensions.AndAlso(
                            properties
                            .Zip(parameters, (mem, var) => EqualityImplementation.EqualsExpression(mem.ReturnType, new IL.LdLoc(thisParam).AccessMember(mem), new IL.LdLoc(var)))),
                    noChangeExpression,
                    factoryCall
                ));
            };

            type.Methods.Add(method);

            return method;
        }

        public static IMethod ImplementOptionalWithMethod(this VirtualType type, IMethod withMethod, IMember[] properties)
        {
            var returnValidationResult = withMethod.ReturnType.FullName == typeof(ValidationResult).FullName;
            var method = CreateSignatureCore(type, withMethod.Parameters.Select(p => (p.Type, p.Name)), isOptional: true, returnValidationResult);

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
