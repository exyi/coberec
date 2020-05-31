using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using ICSharpCode.Decompiler.CSharp.Resolver;
using TS=ICSharpCode.Decompiler.TypeSystem;
using Coberec.CSharpGen.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using Coberec.CoreLib;
using Coberec.MetaSchema;
using M=Coberec.MetaSchema;
using E=Coberec.ExprCS;
using Coberec.ExprCS;
using Xunit;
using Newtonsoft.Json.Linq;

namespace Coberec.CSharpGen.Emit
{
    public static class ObjectConstructionImplementation
    {
        public static MethodDef AddCreateConstructor(this TypeSignature declaringType, EmitContext cx, (string name, FieldReference field)[] fields)
        {
            var (sgn, body) = CreateConstructorCore(declaringType, cx, fields, privateNoValidationVersion: false);
            return MethodDef.CreateWithArray(sgn, args => body(args.EagerSelect(Expression.Parameter)));
        }
        static (MethodSignature sgn, Func<ImmutableArray<Expression>, Expression> body) CreateConstructorCore(TypeSignature declaringType, EmitContext cx, (string name, FieldReference field)[] fields, bool privateNoValidationVersion, MethodReference validationMethod = null)
        {
            var parameters = fields.Select(f => new MethodParameter(f.field.ResultType(), f.name));
            if (privateNoValidationVersion)
                parameters = parameters.Prepend(new MethodParameter(TypeReference.FromType(typeof(NoNeedForValidationSentinel)), "_"));

            var accessibility = privateNoValidationVersion ? Accessibility.APrivate : Accessibility.APublic;
            var ctor = MethodSignature.Constructor(declaringType, accessibility, parameters);
            var indexOffset = privateNoValidationVersion ? 1 : 0;
            return (ctor, p => {
                Expression @this = p[0];
                var statements =
                    fields.Select((f, index) => {
                        return @this.AccessField(f.field)
                               .ReferenceAssign(p[index + 1 + indexOffset]);
                    })
                    .Prepend(Expression.MethodCall(MethodSignature.Object_Constructor, ImmutableArray<Expression>.Empty, @this.Box())) // TODO!
                    ;
                if (validationMethod is object)
                    statements = statements.Append(
                        Expression.StaticMethodCall(validationMethod)
                        .CallMethod(MethodReference.FromLambda<ValidationErrors>(e => e.ThrowErrors("", null)),
                                    Expression.Constant($"Could not initialize {declaringType.Name} due to validation errors"),
                                    @this.Box()
                        )
                    );

                return statements.ToBlock();
            });
        }
        public static (MethodDef main, MethodDef benevolentVariant) AddValidatingConstructor(TypeSignature declaringType, EmitContext cx, MethodReference baseConstructor, MethodReference validationMethod, ImmutableArray<Expression> defaultValues)
        {
            Assert.Equal(baseConstructor.Params()[0].Type, TypeReference.FromType(typeof(NoNeedForValidationSentinel)));

            var parameters = baseConstructor.Params().Skip(1).ToArray();
            var ctor = MethodSignature.Constructor(declaringType, Accessibility.APublic, parameters);
            return CreateBenevolentMethod(ctor, defaultValues, p => {
                Expression @this = p[0];
                var baseCall = @this.CallMethod(baseConstructor, p.Skip(1).Prepend(Expression.Default(TypeReference.FromType(typeof(NoNeedForValidationSentinel)))).ToArray());

                return new [] {
                    baseCall,
                    Expression.StaticMethodCall(validationMethod, @this)
                    .CallMethod(MethodReference.FromLambda<ValidationErrors>(e => e.ThrowErrors("", null)),
                                Expression.Constant($"Could not initialize {declaringType.Name} due to validation errors"),
                                @this.Box()
                    )
                }.ToBlock();
            });
        }

        static readonly MethodSignature Method_ToImmutableArray = MethodReference.FromLambda(() => ImmutableArray.ToImmutableArray(Enumerable.Empty<int>())).Signature;
        static readonly TypeSignature Type_ImmutableArrayOfT = TypeSignature.FromType(typeof(ImmutableArray<int>));

        static MethodDef CreateBenevolentDef(MethodSignature main, MethodSignature benevolent, ImmutableArray<Func<Expression, Expression>> transforms)
        {
            return MethodDef.CreateWithArray(benevolent, args_ => {
                var (target, args) = main.IsStatic ? (null, args_)
                                                   : (args_[0].Read(), args_.EagerSlice(skip: 1));
                return Expression.MethodCall(
                    main.SpecializeFromDeclaringType(benevolent.TypeParameters.EagerSelect(TypeReference.GenericParameter)),
                    args.EagerZip(transforms, (a, t) => t(a)),
                    target
                );
            });
        }

        static (MethodSignature method, ImmutableArray<Func<Expression, Expression>> transforms, bool isDifferentSignature) CreateBenevolentSignature(MethodSignature method, ImmutableArray<Expression> defaults)
        {
            var p = method.Params.EagerZip(defaults, InduceDefaultValue);
            var newMethod = method.With(@params: p.EagerSelect(p => p.newParameter));
            var isDifferent = !newMethod.Params.EagerSelect(p => p.Type).SequenceEqual(method.Params.EagerSelect(p => p.Type));
            if (isDifferent)
                newMethod = newMethod.Clone();
            return (newMethod, p.EagerSelect(p => p.transform), isDifferent);
        }

        /// Either adds default values to the parameters or add entire new overload that is more benevolent
        static (MethodDef main, MethodDef benevolentOverload) CreateBenevolentMethod(MethodSignature method, ImmutableArray<Expression> defaults, Func<ImmutableArray<Expression>, Expression> createBody, XmlComment doccomment = null)
        {
            var (method2, transforms, isDifferent) = CreateBenevolentSignature(method, defaults);
            if (isDifferent)
            {
                var main = MethodDef.CreateWithArray(method, args => createBody(args.EagerSelect(Expression.Parameter)));
                var benevolent = CreateBenevolentDef(main.Signature, method2, transforms);
                return (main.With(doccomment: doccomment), benevolent.With(doccomment: doccomment));
            }
            else
            {
                if (!method2.IsStatic)
                    transforms = transforms.Insert(0, e => e);
                var main = MethodDef.CreateWithArray(method2, args => createBody(args.EagerZip(transforms, (a, t) => t(a))));
                return (main.With(doccomment: doccomment), null);
            }
        }

        public static (MethodParameter newParameter, Func<Expression, Expression> transform) InduceDefaultValue(MethodParameter parameter, Expression value)
        {
            var type = parameter.Type;
            var nullUnwrappedType = type.UnwrapNullableValueType() ?? type;
            if (value is null)
            {
                if (type.IsGenericInstanceOf(Type_ImmutableArrayOfT)) // TODO: nullable types (depends on sane decompilation of conditionals)
                {
                    // ImmutableArray<X>
                    var t = ((TypeReference.SpecializedTypeCase)nullUnwrappedType).Item.GenericParameters.Single();
                    Expression nonNullable(Expression e) => e.CallMethod(Method_ToImmutableArray.SpecializeFromDeclaringType(t));
                    Expression nullable(Expression e) =>
                        Expression.Conditional(
                            e.IsNull(),
                            Expression.Default(type),
                            e.CallMethod(Method_ToImmutableArray.SpecializeFromDeclaringType(t))
                                .Apply(ExpressionFactory.Nullable_Create)
                        );
                    return (parameter.With(type: TypeReference.SpecializedType(TypeSignature.IEnumerableOfT.Specialize(t))),
                            type.IsNullableValueType() ? nullable : (Func<Expression, Expression>)nonNullable);
                }
                return (parameter, e => e);
            }

            Assert.Equal(type, value.Type());
            if (value is Expression.ConstantCase d)
                return (parameter.WithDefault(d.Item.Value), e => e);
            else if (value is Expression.DefaultCase)
                return (parameter.WithDefault(null), e => e);
            else
                throw new NotSupportedException();

        }

        /// <param name="typeSchema"></param>
        public static XmlComment CreateCtorDocumentation(M.TypeDef typeSchema, int unionCase = -1, string text = "Creates new instance of")
        {
            var summary =
                typeSchema.Description is string description ?
                $"<summary> {text} {typeSchema.Name}: {description} </summary>\n" :
                null;
            var props =
                typeSchema.Core.Match(
                    primitive: p => new [] { ("value", (string)null) },
                    union: p => new (string, string)[] { },
                    @interface: i => i.Fields.Select(f => (f.Name,f.Description)),
                    composite: c => c.Fields.Select(f => (f.Name, f.Description))
                );
            var paramComments =
                props.Where(p => p.Description != null)
                     .Select(p => $"<param name=\"{p.Name}\">{p.Description}</param>")
                     .ToArray();
            if (!paramComments.Any() && summary is object)
                return null;
            return new XmlComment(summary + string.Join("\n", paramComments));
        }

        public static (MethodDef noValidationConstructor, MethodDef validatingConstructor, MethodDef benevolentConstructor, MethodDef validationMethod, ImmutableArray<Expression> defaultValues) AddObjectCreationStuff(
            this TypeSignature type,
            EmitContext cx,
            M.TypeDef typeSchema,
            (M.TypeField schema, FieldReference field)[] fields,
            IEnumerable<ValidatorUsage> validators,
            bool needsNoValidationConstructor,
            MethodSignature validateMethodExtension
        )
        {
            var defaultValues = GetDefaultParameterValues(fields);

            var ctorComment = CreateCtorDocumentation(typeSchema);
            var fieldNames = fields.Select(f => (f.schema.Name, f.field)).ToArray();
            var validator = ValidationImplementation.ImplementValidateIfNeeded(type, cx, typeSchema, validators, fieldNames, validateMethodExtension);
            var privateNoValidationVersion = needsNoValidationConstructor && validator is object;
            var ctor1core = CreateConstructorCore(type, cx, fieldNames, privateNoValidationVersion, needsNoValidationConstructor ? null : validator.Signature);
            var (validatedCtor, benevolentCtor) =
                privateNoValidationVersion ?
                AddValidatingConstructor(type, cx, ctor1core.sgn, validator.Signature, defaultValues) :
                CreateBenevolentMethod(ctor1core.sgn, defaultValues, ctor1core.body);
            validatedCtor = validatedCtor?.With(doccomment: ctorComment);
            benevolentCtor = benevolentCtor?.With(doccomment: ctorComment);

            var noValCtor = privateNoValidationVersion ? MethodDef.CreateWithArray(ctor1core.sgn, args => ctor1core.body(args.EagerSelect(Expression.Parameter))) :
                            validator is null          ? validatedCtor :
                                                         null;

            return (noValCtor,
                    validatedCtor,
                    benevolentCtor,
                    validator,
                    defaultValues
                   );
        }

        private static ImmutableArray<Expression> GetDefaultParameterValues((TypeField schema, FieldReference field)[] fields)
        {
            var defaultValues = fields.Select((f, i) => {
                if (f.schema.Directives.SingleOrDefault(d => d.Name == "default") is Directive d)
                {
                    Exception error(string message)
                    {
                        var dIndex = f.schema.Directives.IndexOf(d);
                        return new ValidationErrorException(ValidationErrors.CreateField(new[] { "fields", i.ToString(), "directives", dIndex.ToString(), "args", "value" }, message));
                    }
                    var v = d.Args["value"];
                    var ftype = f.field.ResultType();
                    var fntype = ftype.UnwrapNullableValueType() ?? ftype;
                    if (v.Type == JTokenType.Null)
                    {
                        if (ftype.IsReferenceType != true && !ftype.IsGenericInstanceOf(TypeSignature.NullableOfT))
                            throw error($"Default value can't be null, since the field type '{ftype}' is not nullable"); // TODO non-nullable reference types (with different error message)
                        return Expression.Default(ftype);
                    }
                    if (v.Type == JTokenType.String)
                    {
                        if (ftype != TypeSignature.String)
                            throw error($"Default value can't be a string, since the field is of type '{ftype}'"); // TODO scalar types and enums?
                        return Expression.Constant(v.Value<string>(), ftype);
                    }
                    if (v.Type == JTokenType.Integer && fntype == TypeSignature.Int32)
                    {
                        return Expression.Constant(v.Value<int>(), ftype);
                    }
                    if ((v.Type == JTokenType.Integer || v.Type == JTokenType.Float) && fntype == TypeSignature.Double)
                    {
                        return Expression.Constant(v.Value<double>(), ftype);
                    }
                    throw error($"Default value {v} for field of type '{ftype}' is not supported.");
                }
                return null;
            }).ToImmutableArray();
            return defaultValues;
        }

        public static (MethodDef create, MethodDef benevolentCreate) AddCreateFunction(
            this TypeSignature type,
            EmitContext cx,
            MethodReference validationMethod,
            MethodReference constructor,
            ImmutableArray<Expression>? defaultValues,
            M.TypeDef typeSchema)
        {

            var returnType = TypeSignature.FromType(typeof(ValidationResult<>)).Specialize(type);
            var parameters = constructor.Params();
            bool hasSentinelParam = parameters.FirstOrDefault()?.Type == TypeReference.FromType(typeof(NoNeedForValidationSentinel));
            if (hasSentinelParam)
                parameters = parameters.Skip(1).ToImmutableArray();

            var method = MethodSignature.Static("Create", type, Accessibility.APublic, returnType, parameters);

            defaultValues ??= ImmutableArray.Create(new Expression[method.Params.Length]);

            var ctorComment = CreateCtorDocumentation(typeSchema);

            return CreateBenevolentMethod(method, defaultValues.Value, p => {
                var resultLocal = ParameterExpression.Create(type, "result");
                var ctorParams = p.AsEnumerable();
                if (hasSentinelParam)
                    ctorParams = ctorParams.Prepend(Expression.Default(constructor.Params().First().Type));

                var createCall = Expression.NewObject(constructor, ctorParams.ToImmutableArray());

                var returnExpr =
                    validationMethod == null ?
                    Expression.StaticMethodCall(
                        MethodReference.FromLambda(() => ValidationResult.Create("a")).Signature.Specialize(null, new TypeReference[] { type } ),
                        resultLocal
                    ) :
                    Expression.StaticMethodCall(
                        MethodReference.FromLambda(() => ValidationResult.CreateErrorsOrValue(null, "a")).Signature.Specialize(null, new TypeReference[] { type } ),
                        Expression.StaticMethodCall(validationMethod, resultLocal),
                        resultLocal
                    );

                return returnExpr
                       .Where(resultLocal, createCall);
            }, doccomment: ctorComment);
        }
    }
}
