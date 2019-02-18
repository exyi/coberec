using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;

namespace Coberec.MetaSchema
{
    public static class ValidatorAnalyser
    {
        public static ValidatorUsage[] GetValidatorsForType(this TypeDef type, Func<string, string[]> getValidatorArgs, string unionCase = null)
        {
            var directives =
                AnalyzeDirectives(type.Directives, getValidatorArgs);

            var fieldDirectives =
                type.Core.Match(
                    primitive: _ => Enumerable.Empty<ValidatorUsage>(),
                    union: u => Enumerable.Empty<ValidatorUsage>(), //u.Options.Single(o => o.ToString() == unionCase)
                    @interface: i => i.Fields.SelectMany((f, index) => AnalyzeDirectives(f.Directives, getValidatorArgs, fieldName: f.Name, fieldIndex: index)),
                    composite: c => c.Fields.SelectMany((f, index) => AnalyzeDirectives(f.Directives, getValidatorArgs, fieldName: f.Name, fieldIndex: index)));

            return directives.Concat(fieldDirectives).ToArray();
        }

        private static IEnumerable<ValidatorUsage> AnalyzeDirectives(IEnumerable<Directive> directives, Func<string, string[]> getValidatorArgs, string fieldName = null, int? fieldIndex = null)
        {
            var defaultFields = fieldName == null ? ImmutableArray<string>.Empty : ImmutableArray.Create(fieldName);
            var fieldPrefix = fieldName == null ? "" : fieldName + ".";
            return from xx in directives.Select((d,i) => (i, d))
                   let d = xx.d
                   let dindex = xx.i
                   where d.Name.StartsWith("validate") && d.Name.Length > "validate".Length
                   let validatorName = char.ToLower(d.Name["validate".Length]) + d.Name.Substring("validate".Length + 1)

                   let args = getValidatorArgs(validatorName) ??
                       throw new ValidationErrorException(ValidationErrors.CreateField(
                           (fieldIndex == null ? Array.Empty<string>() : new[] { "fields", fieldIndex.ToString() }).Concat(new [] { "directives", dindex.ToString(), "name"}).ToArray(),
                           $"Validator {validatorName} could not be found"
                       ))

                   let forFields = d.Args["forFields"] == null || args.Contains("forFields") ?
                                   defaultFields :
                                   d.Args["forFields"].Values<string>().Select(a => fieldPrefix + a).ToImmutableArray()

                   let argValues = args.Select(a => (a, val: d.Args[a]))
                                       .Where(a => a.val != null)
                                       .ToDictionary(a => a.a, a => a.val)

                   select new ValidatorUsage(validatorName, argValues, forFields, dindex, fieldIndex);
        }
    }
}
