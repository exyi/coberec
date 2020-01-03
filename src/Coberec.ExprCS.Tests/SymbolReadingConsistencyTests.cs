using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ICSharpCode.Decompiler.CSharp;
using Xunit;

namespace Coberec.ExprCS.Tests
{

    public class SymbolReadingConsistencyTests
    {
        readonly MetadataContext cx = MetadataContext.Create("MyMainModule");
        public static readonly Assembly[] AllAssemblies = new [] {
            typeof(string).Assembly,
            typeof(System.Collections.StructuralComparisons).Assembly,
            typeof(ValueTuple<int, int>).Assembly,
            typeof(Uri).Assembly,
            typeof(ImmutableArrayExtensions).Assembly,
            typeof(Span<int>).Assembly,
            typeof(CSharpDecompiler).Assembly,
            typeof(Assert).Assembly,
            typeof(CoreLib.ValidationErrors).Assembly,
            typeof(System.Text.RegularExpressions.Regex).Assembly,
            typeof(System.Linq.Expressions.ExpressionType).Assembly,
            typeof(System.ComponentModel.PropertyChangedEventHandler).Assembly,
            typeof(System.IO.Directory).Assembly,
            typeof(System.Xml.Linq.XElement).Assembly,
            typeof(System.ComponentModel.ITypeDescriptorContext).Assembly,
            typeof(System.Linq.IGrouping<string, int>).Assembly,
            typeof(System.Collections.Generic.ISet<int>).Assembly,
        }.Distinct().ToArray();

        // public static readonly Type[] AllTypes = AllAssemblies.SelectMany(a => a.GetExportedTypes()).ToArray();
        public static readonly Type[] AllTypes =
            AllAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.FullName.StartsWith("<")) // There is many types of this name and we can't distinguish them
            .Where(t => !t.FullName.StartsWith("Interop")) // There is many types of this name and we can't distinguish them
            .Where(t => t.FullName != "System.Reflection.MethodSemanticsAttributes") // this type is also in System.Reflection.Metadata
            .Where(t => t.FullName != "System.Collections.HashHelpers")
            .Where(t => t.FullName != "System.Runtime.CompilerServices.IsReadOnlyAttribute")
            .Where(t => t.FullName != "System.Text.ValueStringBuilder")
            .Where(t => t.FullName != "System.Collections.Generic.ArrayBuilder`1")
            .Where(t => t.FullName != "System.Collections.Generic.ValueListBuilder`1")
            .Where(t => t.FullName != "System.LocalAppContextSwitches")
            .Where(t => t.FullName != "System.Marvin")
            .Where(t => t.FullName != "System.Resources.FastResourceComparer")
            .Where(t => t.FullName != "System.SR")
            .Where(t => t.FullName != "System.Threading.PlatformHelper")
            .Where(t => t.FullName != "System.NotImplemented")
            .ToArray();
        public static readonly MethodInfo[] AllMethods = AllTypes.SelectMany(t => t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)).ToArray();
        public static readonly FieldInfo[] AllFields = AllTypes.SelectMany(t => t.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)).ToArray();
        public static readonly PropertyInfo[] AllProps = AllTypes.SelectMany(t => t.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)).Where(p => p.GetIndexParameters().Length == 0).ToArray();

        [Fact]
        public void NestedGenericParameter()
        {
            var m = MethodReference.FromLambda<ArraySegment<int>.Enumerator>(e => e.Current);
            var s = m.Signature;
            Assert.Empty(s.DeclaringType.TypeParameters);
            Assert.Equal(s.ResultType, ((TypeOrNamespace.TypeSignatureCase)s.DeclaringType.Parent).Item.TypeParameters.Single());
        }

        public static IEnumerable<object[]> Method_Tests => AllMethods.Select(x => new object[] { x });
        [Theory]
        [MemberData(nameof(Method_Tests))]
        public void MethodLoaderConsistency(MethodInfo m)
        {
            var signature = MethodSignature.FromReflection(m);
            var ilSpy = cx.GetMethod(signature);
            Assert.Equal(SymbolFormatter.MethodToString(m), SymbolFormatter.MethodToString(ilSpy));
            Assert.Equal(signature, SymbolLoader.Method(ilSpy));
        }

        public static IEnumerable<object[]> Type_Tests => AllTypes.Select(x => new object[] { x });

        [Theory]
        [MemberData(nameof(Type_Tests))]
        public void TypeLoaderConsistency(Type t)
        {
            var signature = TypeSignature.FromType(t);
            var ilSpy = cx.GetTypeDef(signature);
            Assert.Equal(SymbolFormatter.TypeDefToString(t), SymbolFormatter.TypeDefToString(ilSpy));
            Assert.Equal(signature, SymbolLoader.Type(ilSpy));
        }

        public static IEnumerable<object[]> Field_Tests => AllFields.Select(x => new object[] { x });

        [Theory]
        [MemberData(nameof(Field_Tests))]
        public void FieldLoaderConsistency(FieldInfo t)
        {
            var signature = FieldSignature.FromReflection(t);
            var ilSpy = cx.GetField(signature);
            Assert.Equal(signature, SymbolLoader.Field(ilSpy));
        }

        public static IEnumerable<object[]> Props_Tests => AllProps.Select(x => new object[] { x });

        [Theory]
        [MemberData(nameof(Props_Tests))]
        public void PropertyLoaderConsistency(PropertyInfo t)
        {
            var signature = PropertySignature.FromReflection(t);
            var ilSpy = cx.GetProperty(signature);
            Assert.Equal(signature, SymbolLoader.Property(ilSpy));
        }
    }
}
