using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TS=ICSharpCode.Decompiler.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using Coberec.CoreLib;
using Coberec.MetaSchema;
using M=Coberec.MetaSchema;
using Coberec.Utils;
using Coberec.ExprCS;
using Xunit;

namespace Coberec.CSharpGen.Emit
{
    public static class ToStringImplementation
    {
        static MethodSignature FormatMethodSignature(TypeSignature declaringType) =>
            MethodSignature.Override(declaringType, MethodReference.FromLambda<ITokenFormatable>(t => t.Format()).Signature);

        public static MethodDef ImplementToString(TypeSignature declaringType)
        {
            var method = MethodSignature.Override(declaringType, MethodSignature.Object_ToString);
            return MethodDef.Create(method, @this => {
                return
                    @this.Ref()
                    .CallMethod(FormatMethodSignature(declaringType))
                    .CallMethod(MethodReference.FromLambda<FmtToken>(x => x.ToString("\t", null)));
            });
        }

        public static MethodDef ImplementFormat(TypeSignature declaringType, M.TypeDef typeDef, (TypeField schema, FieldReference field)[] fields)
        {
            var method = FormatMethodSignature(declaringType);
            if (typeDef.Directives.Any(d => d.Name == "customFormat"))
                return MethodDef.Create(method, @this => new DefaultExpression(method.ResultType));

            var formatDirective = typeDef.Directives.SingleOrDefault(d => d.Name == "format");
            var format = formatDirective?.Args?.Value<string>("t");

            if (format is null)
            {
                format = typeDef.Name + " {{" + string.Join(", ", fields.Select(f => f.schema.Name + " = {" + f.schema.Name + "}")) + "}}";
            }

            var fmt = ParseFormat(format).Apply(MergeLiterals).ToImmutableArray();
            var tokenNames =
                fmt.Select(fr => fr.kind switch {
                    FragmentKind.Field => fr.val,
                    FragmentKind.Literal => ""
                })
                .Select(Expression.Constant)
                .Apply(ExpressionFactory.MakeArray);

            return MethodDef.Create(method, @this => {
                var expressions = fmt.Select(fr => fr.kind switch {
                    FragmentKind.Literal => Expression.Constant(fr.val),
                    FragmentKind.Field =>
                        fields.FirstOrDefault(f => f.schema.Name == fr.val).field is FieldReference field ?
                        FormatField(@this, field) :
                        throw new ValidationErrorException(
                            ValidationErrors.CreateField(new [] { "directives", typeDef.Directives.IndexOf(formatDirective).ToString() }, $"Field '{fr.val}' does not exist")
                        ),
                    _ => throw null
                }).ToImmutableArray();

                return Expression.StaticMethodCall(
                    ConcatMethod,
                    ExpressionFactory
                        .MakeImmutableArray(TypeSignature.Object, expressions.Select(FluentExpression.Box)),
                    tokenNames
                );
            });
        }

        static readonly MethodSignature ConcatMethod = MethodReference.FromLambda(() => FmtToken.Concat(ImmutableArray<object>.Empty, new string[0])).Signature;
        static readonly MethodSignature FormatArrayMethod = MethodReference.FromLambda(() => FmtToken.FormatArray(ImmutableArray<int>.Empty, "", "")).Signature;
        static readonly MethodSignature FormatNullableArrayMethod = MethodReference.FromLambda(() => FmtToken.FormatArray(default(Nullable<ImmutableArray<int>>))).Signature;

        static Expression FormatField(ParameterExpression @this, FieldReference field)
        {
            var access = @this.Ref().ReadField(field);
            var isNullable = access.Type().IsNullableValueType();
            var nunwrap = access.Type().UnwrapNullableValueType() ?? field.ResultType();
            if (nunwrap.IsGenericInstanceOf(TypeSignature.ImmutableArrayOfT))
            {
                var elemType = nunwrap.MatchST(st => st.TypeArguments.Single(), otherwise: null);
                return Expression.StaticMethodCall(
                    (isNullable ? FormatNullableArrayMethod : FormatArrayMethod).Specialize(elemType),
                    access
                );
            }
            else
            {
                return access;
            }
        }

        static IEnumerable<(FragmentKind kind, string val)> MergeLiterals(IEnumerable<(FragmentKind kind, string val)> fragments)
        {
            var constant = "";
            foreach (var (kind, val) in fragments)
            {
                if (kind == FragmentKind.Field)
                {
                    if (constant.Length > 0)
                        yield return (FragmentKind.Literal, constant);
                    constant = "";
                    yield return (kind, val);
                }
                else
                {
                    constant += val;
                }
            }
            if (constant.Length > 0)
                yield return (FragmentKind.Literal, constant);
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
                else if (i < format.Length)
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
