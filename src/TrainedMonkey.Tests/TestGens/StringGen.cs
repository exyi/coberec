using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FsCheck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Coberec.MetaSchema;
using Coberec.CSharpGen;
using IO = System.IO;

namespace Coberec.Tests.TestGens
{
    public static class StringConstants
    {
        static readonly Lazy<string[]> blns = new Lazy<string[]>(() =>
            (string[])new JsonSerializer().Deserialize(
                new IO.StreamReader(typeof(StringConstants).Assembly.GetManifestResourceStream("Coberec.Tests.blns.json")),
                typeof(string[])
            )
        );
        static readonly Lazy<string[]> customStrings = new Lazy<string[]>(() =>
            (string[])new JsonSerializer().Deserialize(
                new IO.StreamReader(typeof(StringConstants).Assembly.GetManifestResourceStream("Coberec.Tests.custom_strings.json")),
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
        public static Gen<string> GenOrPickString =>
            Gen.OneOf(Arb.Default.String().Generator.Where(s => s!=null), Gen.Elements(StringConstants.AllNaughtyStrings));
        public static IEnumerable<string> Shrinker(string _arg1)
            => Arb.Default.String().Shrinker(_arg1);
        public static IEnumerable<string> Shrinker(string arg1, string regex)
        {
            if (string.IsNullOrEmpty(arg1)) return new string[0];
            var shrinkCandidates = Shrinker(arg1).Concat(new [] { arg1.Remove(arg1.Length-1), arg1.Substring(1) });
            return from c in shrinkCandidates
                   let m = Regex.Match(c, regex)
                   where m.Success
                   select m.Value;
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

    public sealed class AnyString
    {
        public AnyString(string val)
        {
            Val = val ?? throw new ArgumentNullException(nameof(val));
        }

        public string Val { get; }
        public override string ToString() => Val;
    }

    public sealed class GraphqlArgs
    {
        public const string NameRegex = "[A-Za-z][A-Za-z0-9_]*";
        public GraphqlArgs(JObject obj)
        {
            // Debug.Assert(Regex.IsMatch(name, "^" + NameRegex + "$"));
            Obj = obj ?? throw new ArgumentNullException(nameof(obj));
        }

        public JObject Obj { get; }
        public override string ToString() => Obj.ToString();
    }
    public static class MyArbs
    {
        private static ConditionalWeakTable<object, object> convertBackTable = new ConditionalWeakTable<object, object>();
        private static Arbitrary<To> MapArb<To, From>(Arbitrary<From> arb, Func<From, To> func)
            where To : class =>
            Arb.Convert<From, To>(
                FsFunc.Create<From, To>(a => {
                    var r = func(a);
                    convertBackTable.GetValue(r, _ => a);
                    return r;
                }),
                FsFunc.Create<To, From>(b => convertBackTable.TryGetValue(b, out var result)
                                   ? (From)result
                                   : throw new Exception()),
                arb
            );

        private static Arbitrary<T> FilterArb<T>(Arbitrary<T> arb, Func<T, bool> predicate) =>
            Arb.From(arb.Generator.Where(predicate), arb.Shrinker);

        private static Arbitrary<To> CastArb<To, From>(Arbitrary<From> arb) =>
            Arb.Convert(FsFunc.Create<From, To>(x => (To)(object)x),
                        FsFunc.Create<To, From>(x => (From)(object)x),
                        arb);

        static ConditionalWeakTable<object[], ConditionalWeakTable<object, StrongBox<int>>> arbAddressBack = new ConditionalWeakTable<object[], ConditionalWeakTable<object, StrongBox<int>>>();
        private static Arbitrary<T> OneOf<T>(Arbitrary<T>[] arb)
        {
            var addrBack = arbAddressBack.GetOrCreateValue(arb);
            return Arb.From<T>(
                Gen.OneOf<T>(arb.Select((a, index) => a.Generator.Select(g => { addrBack.GetValue(g, _ => new StrongBox<int>(index)); return g; }))),
                a => addrBack.TryGetValue(a, out var result) ? arb[result.Value].Shrinker(a).Select(g => { addrBack.GetValue(g, _ => result); return g; })
                                                             : throw new Exception("wtf")
            );
        }

        private static Arbitrary<T> LazyArb<T>(Lazy<Arbitrary<T>> arbitrary)
        {
            return Arb.From<T>(
                Arb.Default.Bool().Generator.SelectMany(m => arbitrary.Value.Generator),
                x => arbitrary.Value.Shrinker(x)
            );
        }

        // private static Arbitrary<(A, B)> ZipArb<A, B>(Arbitrary<A> a, Arbitrary<B> b)
        // {
        //     return Arb.From(
        //         Gen.Map2(FsFunc.Create((A valA, B valB) => (valA, valB)), a.Generator, b.Generator),
        //         FsFunc.Create(((A, B) val) => {
        //         })
        //     );
        // }

        public static Arbitrary<GraphqlName> NameArb => Arb.From(
            StringGenUtils.GenOrPickName.Select(n => new GraphqlName(n)),
            arg => StringGenUtils.Shrinker(arg.Name, GraphqlName.NameRegex).Select(n => new GraphqlName(n))
        );

        public static Arbitrary<AnyString> AnyStringArb => Arb.From(
            StringGenUtils.GenOrPickString.Select(n => new AnyString(n)),
            arg => StringGenUtils.Shrinker(arg.Val, GraphqlName.NameRegex).Select(n => new AnyString(n))
        );

        private static Gen<TypeRef> ActualTypeRefGen = StringGenUtils.GenOrPickName.Select(TypeRef.ActualType);
        public static Arbitrary<TypeRef> TypeRefArb =>
            MapArb(
                Arb.From<(GraphqlName typename, bool[] nesting)>(),
                x => {
                    var t = TypeRef.ActualType(x.typename.Name);
                    foreach (var n in x.nesting)
                    {
                        if (n) t = TypeRef.ListType(t);
                        else if (!(t is TypeRef.NullableTypeCase))
                            t = TypeRef.NullableType(t);
                    }
                    return t;
                }
            );
        private static Arbitrary<JToken> StringJValueArb => MapArb(Arb.From<string>(), s => (JToken)JValue.CreateString(s));
        private static Arbitrary<JToken> IntJValueArb => MapArb(Arb.From<int>(), s => (JToken)new JValue(s));
        private static Arbitrary<JToken> FloatJValueArb => MapArb(Arb.From<float>(), s => (JToken)new JValue(s));
        private static Arbitrary<JToken> BoolJValueArb => MapArb(Arb.From<bool>(), s => (JToken)new JValue(s));
        private static Arbitrary<JToken> NullJValueArb => MapArb(Arb.From<bool>(), s => (JToken)JValue.CreateNull());
        public static Arbitrary<JArray> JArrayArb => MapArb(Arb.From<JToken[]>(), s => new JArray(s));
        public static Arbitrary<JObject> JObjectArb => MapArb(
            FilterArb(Arb.From<(GraphqlName name, JToken value)[]>(), xs => xs.Length == new HashSet<string>(xs.Select(x => x.name.Name)).Count),
            xs => new JObject(xs.Select(x => new JProperty(x.name.Name, x.value))));

        static Lazy<Arbitrary<JToken>> JTokenArb_lazy = new Lazy<Arbitrary<JToken>>(() =>
                OneOf(new Arbitrary<JToken>[] {
                    StringJValueArb,
                    IntJValueArb,
                    FloatJValueArb,
                    BoolJValueArb,
                    NullJValueArb,
                    CastArb<JToken, JArray>(JArrayArb),
                    CastArb<JToken, JObject>(JObjectArb),
                })
            );
        public static Arbitrary<JToken> JTokenArb => LazyArb(JTokenArb_lazy);
        public static Arbitrary<Directive> DirectiveArb => MapArb(Arb.From<(GraphqlName name, JObject args)>(), t => new Directive(t.name.Name, t.args));
        public static Arbitrary<TypeField> TypeFieldArb =>
            MapArb(
                Arb.From<(GraphqlName name, TypeRef type, Directive[] directives)>(),
                t => new TypeField(t.name.Name, t.type, null, t.directives)
            );


        public static Arbitrary<TypeDefCore> TypeDef =>
            OneOf(new [] {
                MapArb(
                    Arb.From<Microsoft.FSharp.Core.Unit>(),
                    _ => TypeDefCore.Primitive()),
                MapArb(
                    Arb.From<TypeField[]>(),
                    x => TypeDefCore.Interface(x.DistinctBy(f => f.Name))),
                MapArb(
                    Arb.From<(TypeField[] fields, GraphqlName[] implements)>(),
                    x => TypeDefCore.Composite(x.fields.DistinctBy(f => f.Name), x.implements.Select(n => TypeRef.ActualType(n.Name)).Distinct())),
                MapArb(
                    Arb.From<NonEmptyArray<GraphqlName>>(),
                    x => TypeDefCore.Union(x.Get.Select(n => TypeRef.ActualType(n.Name)).Distinct())),
            });
        public static Arbitrary<TypeDef> TypeDefArb =>
            MapArb(
                Arb.From<(GraphqlName name, Directive[] dirs, TypeDefCore core)>(),
                x => new TypeDef(x.name.Name, x.dirs, x.core));

        public static Arbitrary<DataSchema> DataSchemaArb =>
            MapArb(
                Arb.From<(TypeDef[] types, DoNotSize<int> seed)>(),
                x => {
                    int random(int seed2, int range) => FsCheck.Random.stdRange(0, range, FsCheck.Random.StdGen.NewStdGen(x.seed.Item, seed2)).Item1;
                    // remove types that have colliding names
                    var types = x.types.DistinctBy(t => t.Name).ToArray();

                    var interfaces =
                       (from t in types
                        let composite = t.Core as TypeDefCore.CompositeCase
                        where composite != null
                        from ifcName in composite.Implements
                        select (ifcName, t, composite)
                        ).ToLookup(xx => ((TypeRef.ActualTypeCase)xx.ifcName).TypeName);

                    // just remove types which name collides with some interface that is going to be created
                    types = types.Where(t => !interfaces.Contains(t.Name)).ToArray();

                    var generatedInterfaces =
                       (from i in interfaces
                        let name = i.Key
                        let maxFields = i.Select(t => t.composite.Fields.Select(f => (f.Name, f.Type)).ToImmutableHashSet()).Aggregate((a, b) => a.Intersect(b))
                        let chosenFields =
                            // maxFields.Where((f, index) => random(index, 2) >= 1)
                            maxFields
                        let core = TypeDefCore.Interface(chosenFields.Select(
                            xx => new TypeField(xx.Name, xx.Type, "", new Directive[0])
                        ))
                        select new TypeDef(name, new Directive[0], core)
                       ).ToArray();

                    return new DataSchema(new Entity[0], types.Concat(generatedInterfaces));
                });
    }
}
