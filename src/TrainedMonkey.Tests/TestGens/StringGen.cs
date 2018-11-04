using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using FsCheck;
using Newtonsoft.Json;
using IO = System.IO;

namespace TrainedMonkey.Tests.TestGens
{
    public static class StringConstants
    {
        static readonly Lazy<string[]> blns = new Lazy<string[]>(() =>
            (string[])new JsonSerializer().Deserialize(
                new IO.StreamReader(typeof(StringConstants).Assembly.GetManifestResourceStream("TrainedMonkey.Tests.blns.json")),
                typeof(string[])
            )
        );
        static readonly Lazy<string[]> customStrings = new Lazy<string[]>(() =>
            (string[])new JsonSerializer().Deserialize(
                new IO.StreamReader(typeof(StringConstants).Assembly.GetManifestResourceStream("TrainedMonkey.Tests.custom_strings.json")),
                typeof(string[])
            )
        );
        static readonly Lazy<string[]> allStrings = new Lazy<string[]>(() => Microsoft.FSharp.Collections.ArrayModule.Append(customStrings.Value, blns.Value));
        public static string[] AllNaughtyStrings => allStrings.Value;

    }

    public static class StringGenUtils
    {
        const int alphaCount = (int)('Z' - 'A' + 1);
        const int alphanumCount = alphaCount * 2 + 10;
        static char mapInt(int a) =>
            a < alphaCount ? (char)('a' + a) :
            a < 2*alphaCount ? (char)('A' + (a - alphaCount)) :
            a < alphanumCount ? (char)('0' + (a - 2*alphaCount)) :
            '_';
        public static Gen<string> GenerateName =>
            Gen.zip(Gen.Choose(0, alphaCount - 1), Gen.ListOf(Gen.Choose(0, alphanumCount))).Select((a) => {

                // name is a non-empty sequence of alphanumeric chars, the first char has to be just alpha
                var result = new string(a.Item2.Prepend(a.Item1).Select(mapInt).ToArray());
                Debug.Assert(Regex.IsMatch(result, "^" + GraphqlName.NameRegex + "$"));
                return result;
            });
        private static Lazy<string[]> NastyGraphqlNames = new Lazy<string[]>(() =>
            (from s in StringConstants.AllNaughtyStrings
             let m = Regex.Match(s, GraphqlName.NameRegex)
             where m.Success
             select m.Value).Distinct()
            .ToArray()
        );
        public static Gen<string> GenOrPickName =>
            Gen.OneOf(StringGenUtils.GenerateName, Gen.Elements(NastyGraphqlNames.Value));
        public static IEnumerable<string> Shrinker(string _arg1)
            => Arb.Default.String().Shrinker(_arg1);
        public static IEnumerable<string> Shrinker(string arg1, string regex)
        {
            var shrinkCandidates = Shrinker(arg1).Concat(new [] { arg1.Remove(arg1.Length-1), arg1.Substring(1) });
            return shrinkCandidates.Where(s => Regex.IsMatch(s, regex));
        }
    }

    public sealed class GraphqlName
    {
        public const string NameRegex = "[A-Za-z][A-Za-z0-9_]*";
        public GraphqlName(string name)
        {
            Debug.Assert(Regex.IsMatch(name, "^" + NameRegex + "$"));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name { get; }
        public override string ToString() => Name;
    }

    public class GraphqlArb : Arbitrary<GraphqlName>
    {
        public override Gen<GraphqlName> Generator => StringGenUtils.GenOrPickName.Select(n => new GraphqlName(n));
        public override IEnumerable<GraphqlName> Shrinker(GraphqlName _arg1) =>
            StringGenUtils.Shrinker(_arg1.Name, GraphqlName.NameRegex).Select(n => new GraphqlName(n));
    }

    public static class MyArbs
    {
        public static Arbitrary<GraphqlName> GraphqlName => new GraphqlArb();
    }
}
