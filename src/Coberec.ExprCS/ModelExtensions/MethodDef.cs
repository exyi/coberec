using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Xunit;

namespace Coberec.ExprCS
{
    public partial class MethodDef
    {
        public MethodDef(MethodSignature signature, IEnumerable<ParameterExpression> args, Expression body)
            : this(signature, args?.ToImmutableArray() ?? ImmutableArray<ParameterExpression>.Empty, body, ImmutableArray<MethodReference>.Empty) { }

        /// <summary> Creates a method definition of a static method without arguments. </summary>
        public static MethodDef Create(MethodSignature signature, Expression body)
        {
            Assert.Empty(signature.Params);
            Assert.True(signature.IsStatic);
            return new MethodDef(signature, ImmutableArray<ParameterExpression>.Empty, body);
        }

        /// <summary> Creates a method definition for the signature. </summary>
        /// <param name="body">A factory function that gets all the arguments in an array and returns the method body. If not static, the `this` parameter is first in the array. </param>
        public static MethodDef CreateWithArray(MethodSignature signature, Func<ImmutableArray<ParameterExpression>, Expression> body)
        {
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

        /// <summary> Creates an empty method definition. Useful when declaring an interface. </summary>
        public static MethodDef InterfaceDef(MethodSignature signature)
        {
            Assert.Equal("interface", signature.DeclaringType.Kind);
            return CreateWithArray(signature, _ => null);
        }
    }
}
