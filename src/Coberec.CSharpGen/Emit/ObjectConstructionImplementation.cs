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

namespace Coberec.CSharpGen.Emit
{
    public static class ObjectConstructionImplementation
    {
        public static MethodDef AddCreateConstructor(this TypeSignature declaringType, EmitContext cx, (string name, FieldReference field)[] fields, bool privateNoValidationVersion, MethodReference validationMethod = null)
        {
            var parameters = fields.Select(f => new MethodParameter(f.field.ResultType(), f.name));
            if (privateNoValidationVersion)
                parameters = parameters.Prepend(new MethodParameter(TypeReference.FromType(typeof(NoNeedForValidationSentinel)), "_"));

            var accessibility = privateNoValidationVersion ? Accessibility.APrivate : Accessibility.APublic;
            var ctor = MethodSignature.Constructor(declaringType, accessibility, parameters);
            var indexOffset = privateNoValidationVersion ? 1 : 0;
            return MethodDef.CreateWithArray(ctor, p => {
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
                        .CallMethod(MethodReference.FromLambda<ValidationErrors>(e => e.ThrowErrors("")),
                                    Expression.Constant($"Could not initialize {declaringType.Name} due to validation errors")
                        )
                    );

                return statements.ToBlock();
            });
        }
        public static MethodDef AddValidatingConstructor(TypeSignature declaringType, EmitContext cx, MethodReference baseConstructor, MethodReference validationMethod)
        {
            Assert.Equal(baseConstructor.Params()[0].Type, TypeReference.FromType(typeof(NoNeedForValidationSentinel)));

            var parameters = baseConstructor.Params().Skip(1).ToArray();
            var ctor = MethodSignature.Constructor(declaringType, Accessibility.APublic, parameters);
            return MethodDef.CreateWithArray(ctor, p => {
                Expression @this = p[0];
                var baseCall = @this.CallMethod(baseConstructor, p.Skip(1).Select(Expression.Parameter).Prepend(Expression.Default(TypeReference.FromType(typeof(NoNeedForValidationSentinel)))).ToArray());

                return new [] {
                    baseCall,
                    Expression.StaticMethodCall(validationMethod, @this)
                    .CallMethod(MethodReference.FromLambda<ValidationErrors>(e => e.ThrowErrors("")),
                                Expression.Constant($"Could not initialize {declaringType.Name} due to validation errors")
                    )
                }.ToBlock();
            });
        }

        public static (MethodDef noValidationConsructor, MethodDef constructor, MethodDef validationMethod) AddObjectCreationStuff(
            this TypeSignature type,
            EmitContext cx,
            M.TypeDef typeSchema,
            TypeSymbolNameMapping typeMapping,
            (string name, FieldReference field)[] fields,
            IEnumerable<ValidatorUsage> validators,
            bool needsNoValidationConstructor,
            MethodSignature validateMethodExtension
        )
        {
            var validator = ValidationImplementation.ImplementValidateIfNeeded(type, cx, typeSchema, typeMapping, validators, fields, validateMethodExtension);
            var privateNoValidationVersion = needsNoValidationConstructor && validator != null;
            var ctor1 = AddCreateConstructor(type, cx, fields, privateNoValidationVersion, needsNoValidationConstructor ? null : validator.Signature);
            var ctor2 = privateNoValidationVersion ?
                        AddValidatingConstructor(type, cx, ctor1.Signature, validator.Signature) :
                        null;

            return (validator == null || needsNoValidationConstructor ? ctor1 : null,
                    ctor2 ?? ctor1,
                    validator
                   );
        }

        public static MethodDef AddCreateFunction(this TypeSignature type,
                                                  EmitContext cx,
                                                  MethodReference validationMethod,
                                                  MethodReference constructor)
        {
            var returnType = TypeSignature.FromType(typeof(ValidationResult<>)).Specialize(type);
            var parameters = constructor.Params();
            bool hasSentinelParam = parameters.FirstOrDefault()?.Type == TypeReference.FromType(typeof(NoNeedForValidationSentinel));
            if (hasSentinelParam)
                parameters = parameters.Skip(1).ToImmutableArray();

            var method = MethodSignature.Static("Create", type, Accessibility.APublic, returnType, parameters);

            return MethodDef.CreateWithArray(method, p => {
                var resultLocal = ParameterExpression.Create(type, "result");
                var ctorParams = p.Select(Expression.Parameter);
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
            });
        }
    }
}
