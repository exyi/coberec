using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using I=ICSharpCode.Decompiler.CSharp.Resolver;
using TS=ICSharpCode.Decompiler.TypeSystem;
using Coberec.CSharpGen.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using Coberec.CoreLib;
using Coberec.MetaSchema;
using Newtonsoft.Json.Linq;
using ObjExpr = System.Func<Coberec.ExprCS.Expression, Coberec.ExprCS.Expression>;
using E=Coberec.ExprCS;
using Coberec.ExprCS;
using M=Coberec.MetaSchema;

namespace Coberec.CSharpGen.Emit
{
    public static class ValidationImplementation
    {
        static void CollectValueArgs(
            Dictionary<MethodParameter, ObjExpr> result,
            ValidatorConfig def,
            string validatorName,
            MethodReference method,
            Func<int, TypeReference, ObjExpr> getField,
            int fieldCount,
            MetadataContext cx
        )
        {
            var parameters = method.Params();
            var valueParameters = (def.ValueParameters ?? ImmutableArray.Create(parameters.Indexed().Single(x => x.value.Name == "value").index))
                                   .Select(i => parameters[i])
                                   .ToArray();
            foreach (var (index, p) in valueParameters.Indexed())
            {
                if (p.IsParams)
                {
                    if (result.ContainsKey(p))
                        throw new Exception($"Parameter {p.Name} was already filled");

                    var elementType = p.Type.GetElementTypeFromIEnumerable(cx, false, out _);
                    var list = new List<ObjExpr>();
                    for (int pi = 0; pi + index < fieldCount; pi++)
                        list.Add(getField(index + pi, elementType));
                    result.Add(p, @this => ExpressionFactory.MakeArray(elementType, list.Select(x => x(@this)).ToArray()));
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
                        if (!p.HasDefaultValue)
                            throw new Exception($"Required value parameter {p.Name} was not filled");
                        var a = Expression.Constant(p.DefaultValue, p.Type); //JsonToObjectInitialization.InitializeObject(p.Type, JToken.FromObject(p.GetConstantValue()));
                        result.Add(p, _ => a);
                    }
                }
            }

            foreach (var p in parameters)
            {
                if (!result.ContainsKey(p))
                {
                    if (p.HasDefaultValue)
                    {
                        var a = Expression.Constant(p.DefaultValue, p.Type);
                        result.Add(p, _ => a);
                    }
                    else
                        throw new Exception($"Method '{method}' registred as validator '{validatorName}' has an unexpected parameter '{p.Name}'.");
                }
            }
        }

        static IEnumerable<ValidatorUsage> GetImplicitNullValidators(M.TypeDef typeSchema, (string name, FieldReference field)[] declaredFields)
        {
            if (typeSchema.Core is TypeDefCore.PrimitiveCase)
            {
                yield return new ValidatorUsage("notNull", new Dictionary<string, JToken>(), new[] { "value" });
            }
            else if (typeSchema.Core is TypeDefCore.CompositeCase comp)
            {
                foreach (var f in comp.Fields)
                {
                    var realProp = declaredFields.Single(t => t.name == f.Name);
                    if (!(f.Type is TypeRef.NullableTypeCase) && realProp.field.ResultType().IsReferenceType == true)
                    {
                        yield return new ValidatorUsage("notNull", new Dictionary<string, JToken>(), new [] { f.Name });
                    }
                }
            }
        }

        static (Expression condition, Expression validator) GetValidatorImplementation(Expression @this, EmitContext cx, ValidatorUsage v, (string name, FieldReference field)[] declaredFields)
        {
            var def = cx.Settings.Validators.GetValueOrDefault(v.Name) ?? throw new ValidationErrorException(ValidationErrors.CreateField(new [] { "name" }, $"Validator {v.Name} could not be found."));
            var method = cx.FindMethod(def.ValidationMethodName);

            if (method.ResultType != E.TypeSignature.FromType(typeof(ValidationErrors))) throw new Exception($"Validation method {def.ValidationMethodName} must return ValidationErrors");

            var argParameters = def.ValidatorParameters.Select(p => {
                var parameter = method.Params[p.parameterIndex];
                return (
                    value: v.Arguments.GetValueOrDefault(p.name) ?? p.defaultValue ?? (parameter.HasDefaultValue ?
                                                                                       JToken.FromObject(parameter.DefaultValue) :
                                                                                       throw new ValidationErrorException(ValidationErrors.CreateField(new [] { "args" }, $"Required parameter {p.name} is missing"))),
                    parameter
                );
            }).ToArray();

            var fields = v.ForFields.IsEmpty ?
                            new [] { new string[0] } :
                            v.ForFields.Select(f => f.Split('.')).ToArray();

            MemberReference getFieldMember(string field) =>
                declaredFields.First(f => f.name == field).field;

            Expression unwrapNullable(Expression expr)
            {
                if (def.AcceptsNull) return expr;

                if (expr.Type().IsNullableValueType())
                    return ExpressionFactory.Nullable_Value(expr);
                return expr;
            }

            Expression accessNullableField(Expression expr, string field)
            {
                var p = getFieldMember(field);
                return unwrapNullable(expr.AccessMember(p));
            }

            ObjExpr makeField(string[] field, TypeReference expectedType)
            {
                var resultType = unwrapNullable(ParameterExpression.Create(field.LastOrDefault()?.Apply(getFieldMember).ResultType() ?? expectedType, "tmp"))
                                      .Type();
                // TODO: check type of the parameter (perform implicit conversion check)
                // var conversion = CSharpConversions.Get(cx.Compilation).ImplicitConversion(resultType, expectedType);
                // TODO: correctness - this check does not have to be enough
                // if (!conversion.IsValid)
                //         throw new ValidationErrorException(ValidationErrors.Create($"Validator '{v.Name}' of type '{expectedType.ReflectionName}' can not be applied on field of type '{resultType.ReflectionName}'"));
                return @this => {
                    // if (field.Length > 1)
                    //     throw new ValidationErrorException(ValidationErrors.Create($"Field paths like '{string.Join(".", field)}' are not supported."));

                    var x = @this;
                    foreach (var f in field)
                        x = accessNullableField(x, f);

                    return x.ReferenceConvert(expectedType);
                };
            }

            Expression createFieldNullcheck(Expression expr, string fieldName)
            {
                if (def.AcceptsNull) return null;

                var member = getFieldMember(fieldName);
                // var fieldType = (typeSchema.Core as TypeDefCore.CompositeCase)?.Fields.Single(f => f.Name == fieldName).Type;
                var fieldExpr = expr.AccessMember(member);
                if (member.ResultType().IsNullableValueType())
                    return ExpressionFactory.Nullable_HasValue(fieldExpr);
                else if (member.ResultType().IsReferenceType == true)
                    return Expression.Binary("!=", fieldExpr.Box(), Expression.Constant<object>(null));
                else return null;
            }

            Expression checkForNulls(Expression @this)
            {
                var nullchecks = fields
                                 .Where(f => f.Length > 0)
                                 .Select(f => createFieldNullcheck(@this, f.Single()))
                                 .Where(f => f != null)
                                 .ToArray();
                return Expression.AndAlso(nullchecks);
            }

            var parameters = new Dictionary<MethodParameter, ObjExpr>();
            foreach (var (value, p) in argParameters)
            {
                try
                {
                    var obj = value.ToObject(Type.GetType(cx.Metadata.GetTypeReference(p.Type).FullName));
                    parameters.Add(p, _ => Expression.Constant(obj, p.Type));
                }
                catch(Exception e)
                {
                    throw new ValidationErrorException(ValidationErrors.Create($"Value '{value.ToString()}' is not supported when type {p.Type} is expected."), "Value conversion has failed", e);
                }
                // TODO: maybe support more types than primitives
            }

            CollectValueArgs(parameters, def, v.Name, method, (fieldIndex, expectedType) => {
                return makeField(fields[fieldIndex], expectedType);
            }, fields.Length, cx.Metadata);


            var call = Expression.StaticMethodCall(
                method,
                method.Params.Select(p => parameters.GetValue(p).Invoke(@this)));
            foreach (var field in fields.First())
            {
                // TODO: properly represent errors of more than one field
                call = Expression.StaticMethodCall(
                            MethodReference.FromLambda<ValidationErrors>(e => e.Nest("")),
                            call,
                            Expression.Constant(field)
                        );
            }
            var condition = checkForNulls(@this);
            return (condition, call);
        }

        static TypeReference Type_Errors = TypeReference.FromType(typeof(ValidationErrors));
        static TypeReference Type_Builder = TypeReference.FromType(typeof(ValidationErrorsBuilder));
        static MethodReference Method_Builder_Add = MethodReference.FromLambda<ValidationErrorsBuilder>(b => b.Add(default));
        static MethodReference Method_Builder_Build = MethodReference.FromLambda<ValidationErrorsBuilder>(b => b.Build());

        public static MethodDef ImplementValidateIfNeeded(TypeSignature type, EmitContext cx, M.TypeDef typeSchema, TypeSymbolNameMapping typeMapping, IEnumerable<ValidatorUsage> validators, (string name, FieldReference field)[] fields)
        {
            var methodParams = new[] { new MethodParameter(type.SpecializeByItself(), "obj") };
            var validationMethod = MethodSignature.Static("ValidateObject", type, Accessibility.APrivate, Type_Errors, methodParams);

            var @this = ParameterExpression.Create(methodParams[0]);
            var validateCalls = CreateValidateCalls(@this, cx, typeSchema, validators, fields);

            if (validateCalls.Count == 0) return null;

            var errorsJoin = MethodReference.FromLambda<ValidationErrors[]>(x => ValidationErrors.Join(x));

            var resultV = ParameterExpression.Create(Type_Builder, "e");

            var body = validateCalls.Count switch {
                1 => Expression.Conditional(validateCalls[0].condition, validateCalls[0].validator, Expression.Default(Type_Errors)),
                _ => validateCalls.Select(v => Expression.IfThen(v.condition, Expression.VariableReference(resultV).CallMethod(Method_Builder_Add, v.validator)))
                     .ToBlock(result: Expression.VariableReference(resultV).CallMethod(Method_Builder_Build))
                     .Where(resultV, Expression.Default(Type_Builder))
            };

            return new MethodDef(validationMethod, new [] { @this }, body);
        }

        private static List<(Expression condition, Expression validator)> CreateValidateCalls(Expression @this, EmitContext cx, M.TypeDef typeSchema, IEnumerable<ValidatorUsage> validators, (string name, FieldReference field)[] fields)
        {
            var validateCalls = new List<(Expression condition, Expression validator)>();
            foreach (var v in Enumerable.Concat(GetImplicitNullValidators(typeSchema, fields), validators))
            {
                try
                {
                    validateCalls.Add(GetValidatorImplementation(@this, cx, v, fields));
                }
                catch (ValidationErrorException e) when (v.DirectiveIndex is int dirIndex)
                {
                    var xx = e.Nest(dirIndex.ToString()).Nest("directives");
                    if (v.FieldIndex is int f)
                        xx = xx.Nest(f.ToString()).Nest("fields");
                    throw xx;
                }
            }

            return validateCalls;
        }
    }
}
