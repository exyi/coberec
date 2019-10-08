using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;
using Xunit;

namespace Coberec.ExprCS
{
    public partial class Expression
    {
        /// <summary> Returns an anonymous function (a lambda) </summary>
        public static Expression Function(Expression body, params ParameterExpression[] args) => Function(body, args.AsEnumerable());
        /// <summary> Returns an anonymous function (a lambda) </summary>
        public static Expression Function(Expression body, IEnumerable<ParameterExpression> args) =>
            Function(args.Select(a => new MethodParameter(a.Type, a.Name)).ToImmutableArray(), // TODO: translate ref to ref
                     args.ToImmutableArray(),
                     body);
    }
}
