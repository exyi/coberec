using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using Xunit;

namespace Coberec.ExprCS
{
	/// <summary> Represents a complete definition of a method. Apart from the (<see cref="TypeDef.Signature" />) contains the implementation (<see cref="Body" />) and attributes </summary>
    public partial class MethodDef
    {
		static partial void ValidateObjectExtension(ref ValidationErrorsBuilder e, MethodDef obj)
        {
            if (obj.Implements.IsDefault)
                e.AddErr("default(ImmutableArray<...>) is not allowed value", "implements");

            var sgn = obj.Signature;
            if (obj.Body is object && obj.Body.Type() != sgn.ResultType)
                e.Add(ValidationErrors.Create($"Method body was expected to return {sgn.ResultType}, not {obj.Body.Type()}").Nest("body")); // TODO: expression type validation
            var isWithoutBody = obj.Signature.IsAbstract || obj.Signature.DeclaringType.Kind == "interface";
            if (obj.Body is null && !isWithoutBody)
                e.Add(ValidationErrors.Create($"Method is not abstract, so it must contain a body."));
            if (obj.Body is object && isWithoutBody)
                e.Add(ValidationErrors.Create($"Method is abstract, so it must not contain a body. Did you intent to use MethodDef.InterfaceDef?"));

            if (!isWithoutBody)
            {
                if (obj.ArgumentParams.IsDefault)
                    e.AddErr("default(ImmutableArray<...>) is not allowed value", "argumentParams");

                var expectedArgs = obj.Signature.Params.Select(p => p.Type).ToImmutableArray();
                if (!sgn.IsStatic)
                    expectedArgs = expectedArgs.Insert(0, sgn.DeclaringType.SpecializeByItself());
                if (obj.ArgumentParams.Length != expectedArgs.Length)
                    e.Add(ValidationErrors.Create($"Expected {expectedArgs.Length} arguments, got {obj.ArgumentParams.Length}: {FmtToken.FormatArray(obj.ArgumentParams)}").Nest("length").Nest("argumentParams"));

                for (int i = 0; i < Math.Min(obj.ArgumentParams.Length, expectedArgs.Length); i++)
                {
                    if (obj.ArgumentParams[i].Type.UnwrapReference() != expectedArgs[i].UnwrapReference())
                        e.Add(ValidationErrors.Create($"Argument type {expectedArgs[i]} was expected instead of {obj.ArgumentParams[i].Type}.")
                            .Nest("type").Nest(i.ToString()).Nest("argumentParams")
                        );
                }

                if (!sgn.IsStatic)
                {
                    var firstArgument = obj.ArgumentParams.First();
                    if (sgn.DeclaringType.IsValueType)
                    {
                        if (TypeReference.ByReferenceType(sgn.DeclaringType.SpecializeByItself()) != firstArgument.Type)
                            e.AddErr($"Method on value type should have this of a reference type: {firstArgument.Type}&", "argumentParams", "0", "type");
                    }
                }

            }

            // TODO: deep validate expression in the context
        }

        public MethodDef(MethodSignature signature, IEnumerable<ParameterExpression> args, Expression body)
            : this(signature, args?.ToImmutableArray() ?? ImmutableArray<ParameterExpression>.Empty, body, ImmutableArray<MethodReference>.Empty) { }

        /// <summary> Creates a method definition of a static method without arguments. </summary>
        public static MethodDef Create(MethodSignature signature, Expression body)
        {
            return new MethodDef(signature, ImmutableArray<ParameterExpression>.Empty, body);
        }

        /// <summary> Creates a method definition for the signature. </summary>
        /// <param name="body">A factory function that gets all the arguments in an array and returns the method body. If not static, the `this` parameter is first in the array. </param>
        public static MethodDef CreateWithArray(MethodSignature signature, Func<ImmutableArray<ParameterExpression>, Expression> body)
        {
            _ = signature ?? throw new ArgumentNullException(nameof(signature));
            _ = body ?? throw new ArgumentNullException(nameof(body));

            var args = signature.Params.Select(ParameterExpression.Create);
            if (!signature.IsStatic)
                args = args.Prepend(ParameterExpression.CreateThisParam(signature.DeclaringType));
            var argsA = args.ToImmutableArray();
            var bodyExpr = body(argsA);
            return new MethodDef(signature, argsA, bodyExpr);
        }

        /// <summary> Creates a method definition for the signature. </summary>
        /// <param name="body"> A factory function that gets the single argument if the method is static, or `this` parameter if it is instance. </param>
        public static MethodDef Create(MethodSignature signature, Func<ParameterExpression, Expression> body) =>
            CreateWithArray(signature, args => body(Assert.Single(args)));
        /// <summary> Creates a method definition for the signature. </summary>
        /// <param name="body"> A factory function that gets the two method parameters. If the method is not static, the first one is the `this` parameter. </param>
        public static MethodDef Create(MethodSignature signature, Func<ParameterExpression, ParameterExpression, Expression> body) =>
            CreateWithArray(signature, args => { Assert.Equal(2, args.Length); return body(args[0], args[1]); });
        /// <summary> Creates a method definition for the signature. </summary>
        /// <param name="body"> A factory function that gets the three method parameters. If the method is not static, the first one is the `this` parameter. </param>
        public static MethodDef Create(MethodSignature signature, Func<ParameterExpression, ParameterExpression, ParameterExpression, Expression> body) =>
            CreateWithArray(signature, args => { Assert.Equal(3, args.Length); return body(args[0], args[1], args[2]); });

        /// <summary> Creates an empty method definition. Useful when declaring an interface or an abstract method. </summary>
        public static MethodDef InterfaceDef(MethodSignature signature)
        {
            return CreateWithArray(signature, _ => null);
        }

        /// <summary> Marks the method definition as implementation of the specified interface methods. </summary>
        public MethodDef AddImplements(params MethodReference[] interfaceMethods) =>
            this.With(implements: this.Implements.AddRange(interfaceMethods));
    }
}
