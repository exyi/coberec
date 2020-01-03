using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TS=ICSharpCode.Decompiler.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using Coberec.CoreLib;
using Coberec.MetaSchema;
using M=Coberec.MetaSchema;
using E=Coberec.ExprCS;
using Coberec.ExprCS;
using Xunit;

namespace Coberec.CSharpGen.Emit
{
    public static class ToStringImplementation
    {
        public static MethodDef ImplementToString(TypeSignature declaringType, EmitContext cx, M.TypeDef typeDef, (TypeField schema, FieldReference field)[] fields)
        {
            if (typeDef.Directives.Any(d => d.Name == "customFormat")) return null;

            var formatDirective = typeDef.Directives.SingleOrDefault(d => d.Name == "format");
            var format = formatDirective?.Args?.Value<string>("t");

            if (format is null)
            {
                format = typeDef.Name + " {{" + string.Join(", ", fields.Select(f => f.schema.Name + " = {" + f.schema.Name + "}")) + "}}";
            }

            var fmt = ParseFormat(format).ToImmutableArray();

            var method = MethodSignature.Override(declaringType, MethodSignature.Object_ToString);

            return MethodDef.Create(method, @this => {

                var expressions = fmt.SelectMany(fr => fr.kind switch {
                    FragmentKind.Literal => new [] { Expression.Constant(fr.val) },
                    FragmentKind.Field =>
                        fields.FirstOrDefault(f => f.schema.Name == fr.val).field is FieldReference field ?
                        FormatField(@this, field) :
                        throw new ValidationErrorException(
                            ValidationErrors.CreateField(new [] { "directives", typeDef.Directives.IndexOf(formatDirective).ToString() }, $"Field '{fr.val}' does not exist")
                        ),
                    _ => throw null
                }).ToImmutableArray();
                return ExpressionFactory.String_Concat(expressions);
            });
        }

        static Expression[] FormatField(ParameterExpression @this, FieldReference field)
        {
            var access = Expression.FieldAccess(field, Expression.VariableReference(@this)).Dereference();
            var nunwrap = access.Type().UnwrapNullableValueType() ?? field.ResultType();
            if (nunwrap.IsGenericInstanceOf(TypeSignature.ImmutableArrayOfT))
            {
                var elemType = nunwrap.MatchST(st => st.GenericParameters.Single(), otherwise: null);
                return new[] {
                    Expression.Constant("["),
                    Expression.StaticMethodCall(
                        MethodReference.FromLambda(() => string.Join<int>(", ", Enumerable.Empty<int>())).Signature.SpecializeFromDeclaringType(elemType),
                        Expression.Constant(", "),
                        access.ReferenceConvert(TypeSignature.IEnumerableOfT.Specialize(elemType))
                    ),
                    Expression.Constant("]")
                };
            }
            else
            {
                return new [] { access };
            }
        }

        static IEnumerable<(FragmentKind kind, string val)> ParseFormat(string format)
        {
            int i = 0;

            while (i < format.Length)
            {
                int literalStart = i;
                while (i < format.Length && format[i] != '{' && format[i] != '}')
                    i++;

                if (i > literalStart)
                    yield return (FragmentKind.Literal, format.Substring(literalStart, Math.Min(i, format.Length) - literalStart));

                if (i < format.Length - 1 && format[i] == '}' && format[i + 1] == '}')
                {
                    i += 2;
                    yield return (FragmentKind.Literal, "}");
                }
                else if (i < format.Length - 1 && format[i] == '{' && format[i + 1] == '{')
                {
                    i += 2;
                    yield return (FragmentKind.Literal, "{");
                }
                else if (i < format.Length - 1 && format[i] == '{')
                {
                    i++;
                    var fieldStart = i;
                    while (i < format.Length && format[i] != '}')
                        i++;
                    yield return (FragmentKind.Field, format.Substring(fieldStart, Math.Min(i, format.Length) - fieldStart));
                    i++;
                }
                else
                {
                    throw new Exception($"Unexpected {format[i]} in '{format}' at {i}");
                }
            }
        }

        enum FragmentKind
        {
            Literal,
            Field
        }
    }
}
