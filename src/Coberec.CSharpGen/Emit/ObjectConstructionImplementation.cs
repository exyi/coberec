using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp.Resolver;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using Coberec.CSharpGen.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using Coberec.CoreLib;
using Coberec.MetaSchema;

namespace Coberec.CSharpGen.Emit
{
    public static class ObjectConstructionImplementation
    {
        public static IMethod AddCreateConstructor(this VirtualType type, EmitContext cx, (string name, IField field)[] fields, bool privateNoValidationVersion, IMethod validationMethod = null)
        {
            var noValidationSentinelParameter = privateNoValidationVersion ? new [] { new DefaultParameter(cx.FindType<NoNeedForValidationSentinel>(), "_") } : new IParameter[0];
            var parameters = SymbolNamer.NameParameters(noValidationSentinelParameter.Concat(fields.Select(f => (IParameter)new DefaultParameter(f.field.Type, f.name))));
            var accessibility = privateNoValidationVersion ? Accessibility.Private : Accessibility.Public;
            var ctor = new VirtualMethod(type, accessibility, ".ctor", parameters, cx.FindType(typeof(void)));
            var objectCtor = cx.FindMethod(() => new object());
            var indexOffset = privateNoValidationVersion ? 1 : 0;
            ctor.BodyFactory = () =>
            {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                return EmitExtensions.CreateOneBlockFunction(ctor,
                    fields.Zip(parameters, (f, p) =>
                    {
                        var index = Array.IndexOf(parameters, p);
                        return (IL.ILInstruction)new IL.StObj(new IL.LdFlda(new IL.LdLoc(thisParam), f.field), new IL.LdLoc(new IL.ILVariable(IL.VariableKind.Parameter, f.field.ReturnType, index + indexOffset) { Name = p.Name }), f.field.ReturnType);
                    })
                    .Prepend(new IL.Call(objectCtor) { Arguments = { new IL.LdLoc(thisParam) } })
                    .Concat(validationMethod == null ? Enumerable.Empty<IL.ILInstruction>() : new IL.ILInstruction[] {
                        new IL.Call(cx.FindMethod(() => ValidationErrors.Valid.ThrowErrors(""))) { Arguments = {
                            new IL.Call(validationMethod) { Arguments = { new IL.LdLoc(thisParam) } },
                            new IL.LdStr($"Could not initialize {type.Name} due to validation errors")
                        }}
                    })
                    .ToArray()
                );
            };

            type.Methods.Add(ctor);
            return ctor;
        }
        public static IMethod AddValidatingConstructor(this VirtualType type, EmitContext cx, IMethod baseConstructor, IMethod validationMethod)
        {
            Debug.Assert(baseConstructor.Parameters[0].Type.FullName == typeof(NoNeedForValidationSentinel).FullName);
            var parameters = baseConstructor.Parameters.Skip(1).ToArray();
            var ctor = new VirtualMethod(type, Accessibility.Public, ".ctor", parameters, cx.FindType(typeof(void)));
            ctor.BodyFactory = () =>
            {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                var baseCall = new IL.Call(baseConstructor) { Arguments = { new IL.LdLoc(thisParam), new IL.DefaultValue(cx.FindType<NoNeedForValidationSentinel>()) } };
                baseCall.Arguments.AddRange(parameters.Select((p, i) => new IL.LdLoc(new IL.ILVariable(IL.VariableKind.Parameter, p.Type, i))));
                return EmitExtensions.CreateOneBlockFunction(ctor,
                     new IL.ILInstruction[] {
                        baseCall,
                        new IL.Call(cx.FindMethod(() => ValidationErrors.Valid.ThrowErrors(""))) { Arguments = {
                            new IL.Call(validationMethod) { Arguments = { new IL.LdLoc(thisParam) } },
                            new IL.LdStr($"Could not initialize {type.Name} due to validation errors")
                        }}
                    }
                );
            };

            type.Methods.Add(ctor);
            return ctor;
        }

        public static (IMethod noValidationConsructor, IMethod contructor, IMethod validationMethod) AddObjectCreationStuff(
            this VirtualType type,
            EmitContext cx,
            TypeSymbolNameMapping typeMapping,
            (string name, IField field)[] fields,
            IEnumerable<ValidatorUsage> validators,
            bool needsNoValidationConstructor
        )
        {
            var validator = type.ImplementValidateIfNeeded(cx, typeMapping, validators);
            bool privateNoValidationVersion = needsNoValidationConstructor && validator != null;
            var ctor1 = type.AddCreateConstructor(cx, fields, privateNoValidationVersion, needsNoValidationConstructor ? null : validator);
            var ctor2 = privateNoValidationVersion ?
                        type.AddValidatingConstructor(cx, ctor1, validator) :
                        null;

            return (validator == null || needsNoValidationConstructor ? ctor1 : null,
                    ctor2 ?? ctor1,
                    validator
                   );
        }

        public static IMethod AddCreateFunction(this VirtualType type,
                                                EmitContext cx,
                                                IMethod validationMethod,
                                                IMethod constructor)
        {
            var returnType = new ParameterizedType(cx.FindType(typeof(ValidationResult<>)), new [] { type });
            bool hasSentinelParam = constructor.Parameters.FirstOrDefault()?.Type.FullName == typeof(NoNeedForValidationSentinel).FullName;
            var parameters = hasSentinelParam ?
                             constructor.Parameters.Skip(1).ToArray() :
                             constructor.Parameters.ToArray();

            var methodName = SymbolNamer.NameMethod(type, "Create", 0, parameters.Select(p => p.Type).ToArray());
            var method = new VirtualMethod(type, Accessibility.Public, methodName, parameters, returnType, isStatic: true);

            method.BodyFactory = () => {
                var resultLocal = new IL.ILVariable(IL.VariableKind.Local, type, 0);
                var createCall = new IL.NewObj(constructor);
                if (hasSentinelParam)
                    createCall.Arguments.Add(new IL.DefaultValue(constructor.Parameters.First().Type));
                createCall.Arguments.AddRange(parameters.Select((p, i) => new IL.LdLoc(new IL.ILVariable(IL.VariableKind.Parameter, p.Type, i))));

                var returnExpr =
                    validationMethod == null ?
                    (IL.ILInstruction)new IL.Call(cx.FindType(typeof(ValidationResult)).GetMethods(m => m.Name == "Create").Single().Specialize(new TypeParameterSubstitution(null, new [] { type }))) {
                        Arguments = { new IL.LdLoc(resultLocal) }
                    } :
                    new IL.Call(cx.FindType(typeof(ValidationResult)).GetMethods(m => m.Name == "CreateErrorsOrValue").Single().Specialize(new TypeParameterSubstitution(null, new [] { type }))) {
                        Arguments = {
                            new IL.Call(validationMethod) { Arguments = { new IL.LdLoc(resultLocal) } },
                            new IL.LdLoc(resultLocal)
                        }
                    };

                return EmitExtensions.CreateOneBlockFunction(method, new [] {
                    new IL.StLoc(resultLocal, createCall),
                    returnExpr
                });
            };

            type.Methods.Add(method);
            return method;
        }
    }
}
