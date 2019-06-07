using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests
{
    static class Helpers
    {
        public static void AddTestExpr(this MetadataContext cx, Expression expr, params ParameterExpression[] parameters)
        {
            var name = cx.DefinedTypes.Count < 3 ? ((char)('C' + cx.DefinedTypes.Count)).ToString() : "C" + cx.DefinedTypes.Count;
            var ns = ((TypeOrNamespace.NamespaceSignatureCase)cx.DefinedTypes.FirstOrDefault()?.Signature.Parent)?.Item ??
                    new NamespaceSignature("NS", null);
            var type = new TypeSignature(name, ns, false, false, Accessibility.APublic, 0);
            var method = new MethodSignature(type, parameters.Select(p => new MethodArgument(p.Type, p.Name)).ToImmutableArray(), "M", expr.Type(), true, Accessibility.APublic, false, false, false, false, ImmutableArray<GenericParameter>.Empty);
            var methodDef = new MethodDef(method, parameters.ToImmutableArray(), expr);
            var typeDef = TypeDef.Empty(type).With(members: ImmutableArray.Create<MemberDef>(methodDef));

            cx.AddType(typeDef);
        }
    }

    public class ExpressionTranslationTests
    {
        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext MkContext() => MetadataContext.Create("MyModule");

        ParameterExpression p1 = ParameterExpression.Create(TypeSignature.Int32, "p1");
        ParameterExpression p2 = ParameterExpression.Create(TypeSignature.Int32, "p2");

        public ExpressionTranslationTests()
        {
            RaiseExceptionDebugProvider.HackIt();
        }


        [Fact]
        public void ArgumentPassing()
        {
            var cx = MkContext();
            cx.AddTestExpr(p1, p1);
            check.CheckOutput(cx);
        }

        [Fact]
        public void VariableDeclaration()
        {
            var cx = MkContext();
            cx.AddTestExpr(Expression.LetIn(p2, p1, p2), p1);
            check.CheckOutput(cx);
        }
    }
}
