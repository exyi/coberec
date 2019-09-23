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
            : this(signature, args?.ToImmutableArray() ?? ImmutableArray<ParameterExpression>.Empty, body, ImmutableArray<MethodSignature>.Empty) { }

        public static MethodDef Create(MethodSignature signature, Expression body)
        {
            Assert.Empty(signature.Params);
            Assert.True(signature.IsStatic);
            return new MethodDef(signature, ImmutableArray<ParameterExpression>.Empty, body);
        }

        public static MethodDef CreateWithArray(MethodSignature signature, Func<ImmutableArray<ParameterExpression>, Expression> body)
        {
            var args = signature.Params.Select(ParameterExpression.Create);
            if (!signature.IsStatic)
                args = args.Prepend(ParameterExpression.CreateThisParam(signature.DeclaringType));
            var argsA = args.ToImmutableArray();
            var bodyExpr = body(argsA);
            return new MethodDef(signature, argsA, bodyExpr);
        }

        public static MethodDef Create(MethodSignature signature, Func<ParameterExpression, Expression> body) =>
            CreateWithArray(signature, args => body(Assert.Single(args)));
        public static MethodDef Create(MethodSignature signature, Func<ParameterExpression, ParameterExpression, Expression> body) =>
            CreateWithArray(signature, args => { Assert.Equal(2, args.Length); return body(args[0], args[1]); });
        public static MethodDef Create(MethodSignature signature, Func<ParameterExpression, ParameterExpression, ParameterExpression, Expression> body) =>
            CreateWithArray(signature, args => { Assert.Equal(3, args.Length); return body(args[0], args[1], args[2]); });
    }
}
