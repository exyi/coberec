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
using ObjExpr = System.Func<ICSharpCode.Decompiler.IL.ILVariable, ICSharpCode.Decompiler.IL.ILInstruction>;

namespace Coberec.CSharpGen.Emit
{
    public static class ValidationImplementation
    {
        static void CollectValueArgs(
            Dictionary<IParameter, ObjExpr> result,
            ValidatorConfig def,
            string validatorName,
            IMethod method,
            Func<int, IType, ObjExpr> getField,
            int fieldCount
        )
        {
            var valueParameters = (def.ValueParameters ?? ImmutableArray.Create(method.Parameters.Indexed().Single(x => x.value.Name == "value").index))
                                   .Select(i => method.Parameters[i])
                                   .ToArray();
            foreach (var (index, p) in valueParameters.Indexed())
            {
                if (p.IsParams)
                {
                    if (result.ContainsKey(p))
                        throw new Exception($"Parameter {p.Name} was already filled");

                    var elementType = p.Type.GetElementTypeFromIEnumerable(p.Owner.ParentModule.Compilation, false, out _);
                    var list = new List<Func<IL.ILVariable, IL.ILInstruction>>();
                    for (int pi = 0; pi + index < fieldCount; pi++)
                        list.Add(getField(index + pi, elementType));
                    result.Add(p, @this => EmitExtensions.MakeArray(elementType, list.Select(x => x(@this)).ToArray()));
                }
                else if (index < fieldCount)
                {
                    if (result.ContainsKey(p))
                        throw new Exception($"Parameter {p.Name} was already filled");
                    result.Add(p, getField(index, p.Type));
                }
                else
                {
                    if (!result.ContainsKey(p))
                    {
                        if (!p.HasConstantValueInSignature)
                            throw new Exception($"Required value parameter {p.Name} was not filled");
                        var a = JsonToObjectInitialization.InitializeObject(p.Type, JToken.FromObject(p.ConstantValue));
                        result.Add(p, _ => a());
                    }
                }
            }

            foreach (var p in method.Parameters)
            {
                if (!result.ContainsKey(p))
                {
                    if (p.HasConstantValueInSignature)
                    {
                        var a = JsonToObjectInitialization.InitializeObject(p.Type, JToken.FromObject(p.ConstantValue));
                        result.Add(p, _ => a());
                    }
                    else
                        throw new Exception($"Method '{method}' registred as validator '{validatorName}' has an unexpected parameter '{p.Name}'.");
                }
            }
        }

        static IEnumerable<ValidatorUsage> GetImplicitNullValidators(VirtualType type, TypeDef typeSchema, TypeSymbolNameMapping typeMapping)
        {
            if (typeSchema.Core is TypeDefCore.PrimitiveCase)
            {
                yield return new ValidatorUsage("notNull", new Dictionary<string, JToken>(), new[] { "value" });
            }
            else if (typeSchema.Core is TypeDefCore.CompositeCase comp)
            {
                foreach (var f in comp.Fields)
                {
                    var realPropName = typeMapping.Fields[f.Name];
                    var realProp = type.GetProperties(p => p.Name == realPropName).Single();
                    if (!(f.Type is TypeRef.NullableTypeCase) && realProp.ReturnType.IsReferenceType == true)
                    {
                        yield return new ValidatorUsage("notNull", new Dictionary<string, JToken>(), new [] { f.Name });
                    }
                }
            }
        }

        static ObjExpr GetValidatorImplementation(VirtualType type, EmitContext cx, TypeDef typeSchema, TypeSymbolNameMapping typeMapping, ValidatorUsage v)
        {
            var def = cx.Settings.Validators.GetValueOrDefault(v.Name) ?? throw new ValidationErrorException(ValidationErrors.CreateField(new [] { "name" }, $"Validator {v.Name} could not be found."));
            var method = cx.FindMethod(def.ValidationMethodName);

            if (method.ReturnType.FullName != typeof(ValidationErrors).FullName) throw new Exception($"Validation method {def.ValidationMethodName} must return ValidationErrors");

            var argParameters = def.ValidatorParameters.Select(p => {
                var parameter = method.Parameters[p.parameterIndex];
                return (
                    value: v.Arguments.GetValueOrDefault(p.name) ?? p.defaultValue ?? (parameter.HasConstantValueInSignature ?
                                                                                       JToken.FromObject(parameter.ConstantValue) :
                                                                                       throw new ValidationErrorException(ValidationErrors.CreateField(new [] { "args" }, $"Required parameter {p.name} is missing"))),
                    parameter
                );
            }).ToArray();

            var fields = v.ForFields.IsEmpty ?
                            new [] { new string[0] } :
                            v.ForFields.Select(f => f.Split('.')).ToArray();

            IMember getFieldMember(string field) => type.Members.Single(m => m.Name == typeMapping.Fields[field]);

            (IL.ILInstruction, IType) unwrapNullable(IL.ILInstruction expr, IType resultType)
            {
                if (def.AcceptsNull) return (expr, resultType);

                if (resultType.GetDefinition().ReflectionName == typeof(Nullable<>).FullName)
                    return (new IL.Call(resultType.GetProperties(p => p.Name == "Value").Single().Getter) { Arguments = { new IL.AddressOf(expr) } }, resultType.TypeArguments.Single());
                return (expr, resultType);
            }

            (IL.ILInstruction, IType) accessNullableField(IL.ILInstruction expr, string field)
            {
                var p = getFieldMember(field);
                return unwrapNullable(expr.AccessMember(p), p.ReturnType);
            }

            Func<IL.ILVariable, IL.ILInstruction> makeField(string[] field, IType expectedType)
            {
                var (_, resultType) = unwrapNullable(new IL.LdNull(), (field.Select(getFieldMember).LastOrDefault()?.ReturnType ?? expectedType));
                // check type of the parameter
                var conversion = CSharpConversions.Get(cx.Compilation).ImplicitConversion(resultType, expectedType);
                // TODO: correctness - this check does not have to enough
                if (!conversion.IsValid)
                        throw new ValidationErrorException(ValidationErrors.Create($"Validator '{v.Name}' of type '{expectedType.ReflectionName}' can not be applied on field of type '{resultType.ReflectionName}'"));
                return @this => {
                    if (field.Length > 1)
                        throw new ValidationErrorException(ValidationErrors.Create($"Field paths like '{string.Join(".", field)}' are not supported."));

                    (IL.ILInstruction instruction, IType resultType) x = (new IL.LdLoc(@this), type);
                    foreach (var f in field)
                        x = accessNullableField(x.instruction, f);

                    return x.instruction;
                };
            }

            IL.ILInstruction createFieldNullcheck(IL.ILInstruction expr, string fieldName)
            {
                if (def.AcceptsNull) return null;

                var member = getFieldMember(fieldName);
                // var fieldType = (typeSchema.Core as TypeDefCore.CompositeCase)?.Fields.Single(f => f.Name == fieldName).Type;
                var fieldExpr = expr.AccessMember(member);
                if (member.ReturnType.GetDefinition().ReflectionName == typeof(Nullable<>).FullName)
                    return new IL.Call(member.ReturnType.GetProperties(p => p.Name == "HasValue").Single().Getter) { Arguments = { new IL.AddressOf(fieldExpr) } };
                else if (member.ReturnType.IsReferenceType == true)
                    return new IL.Comp(IL.ComparisonKind.Inequality, Sign.None, new IL.IsInst(fieldExpr, cx.Compilation.FindType(KnownTypeCode.Object)), new IL.LdNull());
                else return null;
            }

            IL.ILInstruction checkForNulls(IL.ILVariable @thisParam, IL.ILInstruction core)
            {
                var nullchecks = fields
                                    .Where(f => f.Length > 0)
                                    .Select(f => createFieldNullcheck(new IL.LdLoc(@thisParam), f.Single()))
                                    .Where(f => f != null)
                                    .ToArray();
                if (nullchecks.Length == 0)
                    return core;
                else
                    return new IL.IfInstruction(
                        EmitExtensions.AndAlso(nullchecks),
                        core,
                        new IL.LdNull()
                    );
            }

            var parameters = new Dictionary<IParameter, Func<IL.ILVariable, IL.ILInstruction>>();
            foreach (var (value, p) in argParameters)
            {
                var obj = JsonToObjectInitialization.InitializeObject(p.Type, value);
                parameters.Add(p, _ => obj());
            }

            CollectValueArgs(parameters, def, v.Name, method, (fieldIndex, expectedType) => {
                return makeField(fields[fieldIndex], expectedType);
            }, fields.Length);

            return @this => {
                var call = new IL.Call(method);
                call.Arguments.AddRange(method.Parameters.Select(p => parameters.GetValue(p).Invoke(@this)));
                foreach (var field in fields.First())
                {
                    // TODO: properly represent errors of more than one field
                    call = new IL.Call(cx.FindMethod(() => default(ValidationErrors).Nest(""))) { Arguments = { call, new IL.LdStr(field) } };
                }
                return checkForNulls(@this, call);
            };
        }

        public static VirtualMethod ImplementValidateIfNeeded(this VirtualType type, EmitContext cx, TypeDef typeSchema, TypeSymbolNameMapping typeMapping, IEnumerable<ValidatorUsage> validators)
        {
            var validateCalls = new List<Func<IL.ILVariable, IL.ILInstruction>>();
            foreach(var v in Enumerable.Concat(GetImplicitNullValidators(type, typeSchema, typeMapping), validators))
            {
                try
                {
                    validateCalls.Add(GetValidatorImplementation(type, cx, typeSchema, typeMapping, v));
                }
                catch (ValidationErrorException e) when (v.DirectiveIndex is int dirIndex)
                {
                    var xx = e.Nest(dirIndex.ToString()).Nest("directives");
                    if (v.FieldIndex is int f)
                        xx = xx.Nest(f.ToString()).Nest("fields");
                    throw xx;
                }
            }

            if (validateCalls.Count == 0) return null;

            var methodParams = new IParameter[] { new VirtualParameter(type, "obj") };
            var methodName = SymbolNamer.NameMethod(type, "ValidateObject", 0, methodParams);
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
