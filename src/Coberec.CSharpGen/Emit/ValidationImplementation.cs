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
using Newtonsoft.Json.Linq;

namespace Coberec.CSharpGen.Emit
{
    public static class ValidationImplementation
    {

        public static IMethod ImplementValidateIfNeeded(this VirtualType type, EmitContext cx, TypeSymbolNameMapping typeMapping, IEnumerable<ValidatorUsage> validators)
        {
            var validateCalls = new List<Func<IL.ILVariable, IL.ILInstruction>>();
            foreach(var v in validators)
            {
                var def = cx.Settings.Validators.GetValueOrDefault(v.Name) ?? throw new Exception($"Validator {v.Name} could not be found.");
                var method = cx.FindMethod(def.ValidationMethodName);

                if (method.ReturnType.FullName != typeof(ValidationErrors).FullName) throw new Exception($"Validation method {def.ValidationMethodName} must return ValidationErrors");

                var valueParameters = (def.ValueParameters ?? ImmutableArray.Create(method.Parameters.Indexed().Single(x => x.value.Name == "value").index))
                                      .Select(i => method.Parameters[i])
                                      .ToArray();

                var argParameters = def.ValidatorParameters.Select(p => {
                    var parameter = method.Parameters[p.parameterIndex];
                    return (
                        value: v.Arguments.GetValueOrDefault(p.name) ?? p.defaultValue ?? (parameter.HasConstantValueInSignature ?
                                                                                           JToken.FromObject(parameter.ConstantValue) :
                                                                                           throw new Exception($"Required parameter {p.name} is missing")),
                        parameter
                    );
                }).ToArray();

                var fields = v.ForFields.IsEmpty ?
                             new [] { new string[0] } :
                             v.ForFields.Select(f => f.Split('.')).ToArray();

                IMember getFieldMember(string field) => type.Members.Single(m => m.Name == typeMapping.Fields[field]);

                Func<IL.ILVariable, IL.ILInstruction> makeField(string[] field) =>
                    @this => {
                        IL.ILInstruction x = new IL.LdLoc(@this);
                        foreach (var f in field)
                            x = x.AccessMember(getFieldMember(f));
                        return x;
                    };

                var parameters = new Dictionary<IParameter, Func<IL.ILVariable, IL.ILInstruction>>();
                foreach (var (value, p) in argParameters)
                {

                    parameters.Add(p, _ => JsonToObjectInitialization.InitializeObject(p.Type, value));
                }

                foreach (var (index, p) in valueParameters.Indexed())
                {
                    if (p.IsParams)
                    {
                        if (parameters.ContainsKey(p))
                            throw new Exception($"Parameter {p.Name} was already filled");

                        var list = new List<Func<IL.ILVariable, IL.ILInstruction>>();
                        for (int pi = 0; pi + index < fields.Length; pi++)
                            list.Add(makeField(fields[index + pi]));
                        parameters.Add(p, @this => EmitExtensions.MakeArray(p.Type.GetElementTypeFromIEnumerable(cx.Compilation, false, out _), list.Select(x => x(@this)).ToArray()));
                    }
                    else if (index < fields.Length)
                    {
                        if (parameters.ContainsKey(p))
                            throw new Exception($"Parameter {p.Name} was already filled");
                        parameters.Add(p, makeField(fields[index]));
                    }
                    else
                    {
                        if (!parameters.ContainsKey(p))
                        {
                            if (!p.HasConstantValueInSignature)
                                throw new Exception($"Required value parameter {p.Name} was not filled");
                            parameters.Add(p, _ => JsonToObjectInitialization.InitializeObject(p.Type, JToken.FromObject(p.ConstantValue)));
                        }
                    }
                }

                foreach (var p in method.Parameters)
                {
                    if (!parameters.ContainsKey(p))
                    {
                        if (p.HasConstantValueInSignature)
                            parameters.Add(p, _ => JsonToObjectInitialization.InitializeObject(p.Type, JToken.FromObject(p.ConstantValue)));
                        else
                            throw new Exception($"Method {method} registred as validator {v.Name} has an unexpected parameter {p.Name}.");
                    }
                }

                validateCalls.Add(@this => {
                    var call = new IL.Call(method);
                    call.Arguments.AddRange(method.Parameters.Select(p => parameters.GetValue(p).Invoke(@this)));
                    return call;
                });
            }

            if (validateCalls.Count == 0) return null;

            var methodParams = new IParameter[] { new DefaultParameter(type, "obj") };
            var methodName = SymbolNamer.NameMethod(type, "ValidateObject", 0, methodParams.Select(p => p.Type).ToArray());
            var validationMethod = new VirtualMethod(type, Accessibility.Private, methodName, methodParams, cx.FindType<ValidationErrors>(), isStatic: true);
            validationMethod.BodyFactory = () => {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, 0);

                var errorsJoin = cx.FindMethod(() => ValidationErrors.Join(new ValidationErrors[0]));

                var expression =
                    validateCalls.Count == 1 ?
                    validateCalls.Single().Invoke(thisParam) :
                    new IL.Call(errorsJoin) {
                        Arguments = {
                            EmitExtensions.MakeArray(
                                cx.FindType<ValidationErrors>(),
                                validateCalls.Select(v => v.Invoke(thisParam)).ToArray()
                            )
                        }
                    };


                return EmitExtensions.CreateExpressionFunction(validationMethod, expression);
            };

            type.Methods.Add(validationMethod);
            return validationMethod;
        }
    }
}
