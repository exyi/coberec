using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CheckTestOutput;
using Coberec.CoreLib;
using Xunit;
using Xunit.Abstractions;

namespace Coberec.ExprCS.Tests
{
    public class SymbolReadingTests
    {
        readonly MetadataContext cx = MetadataContext.Create("MyMainModule");
        readonly OutputChecker check = new OutputChecker("testoutput", sanitizeGuids: true);


        readonly ITestOutputHelper output;

        public SymbolReadingTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void LoadCoreTypes()
        {
            var stringT = TypeSignature.FromType(typeof(string));
            var dateTimeT = cx.FindTypeDef("System.DateTime");

            Assert.False(stringT.CanOverride);
            Assert.Empty(stringT.TypeParameters);
            Assert.Equal("String", stringT.Name);
            Assert.Equal("DateTime", dateTimeT.Name);
            Assert.Equal(TypeOrNamespace.NamespaceSignature(NamespaceSignature.System), dateTimeT.Parent);
            Assert.Equal(dateTimeT.Parent, stringT.Parent);

            var enumerableT = TypeSignature.FromType(typeof(IEnumerable<>));

            check.CheckJsonObject(new { stringT, dateTimeT, enumerableT });
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(List<>))]
        public void LoadReflectionType(Type t)
        {
            var signature = TypeSignature.FromType(t);
            var methods = cx.GetMemberMethodDefs(signature).Where(m => m.Accessibility == Accessibility.APublic).Select(m => m.Name).Distinct().OrderBy(a => a).ToArray();
            var reflectionMethods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                     .Where(m => !m.IsSpecialName)
                                     .Select(m => m.Name).Distinct().OrderBy(a => a).ToArray();
            Assert.Equal(methods, reflectionMethods);
        }

        [Fact]
        public void LoadCoreMethods()
        {
            var stringT = TypeSignature.FromType(typeof(string));

            var methods = cx.GetMemberMethods(stringT.NotGeneric()).ToArray();

            var toUpperInvariantM = methods.Single(s => s.Name() == "ToUpperInvariant");

            check.CheckJsonObject(new { toUpperInvariantM });
        }

        [Fact]
        public void LoadGenericFunction()
        {
            var genericMethod = MethodReference.FromLambda(() => ValidationResult.Create("abc")).Signature;
            var genericParent = MethodReference.FromLambda<ValidationResult<string>>(a => a.Expect("a")).Signature;

            check.CheckJsonObject(new { genericMethod, genericParent });
        }

        [Fact]
        public void LoadSpecializeGenericFunction()
        {
            var genericMethod = MethodReference.FromLambda(() => ValidationResult.Create("abc"))
                                .Signature
                                .Specialize(null, new [] { TypeReference.FromType(typeof(int)) });
            var genericParent = MethodReference.FromLambda<ValidationResult<string>>(a => a.Expect("a"))
                                .Signature
                                .Specialize(new [] { TypeReference.FromType(typeof(bool)) }, null);

            check.CheckJsonObject(new { genericMethod_S = genericMethod.ToString(),
                                        genericParent_S = genericParent.ToString(),
                                        genericMethod,
                                        genericParent });
        }

        [Theory]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(Dictionary<int, int>))]
        [InlineData(typeof(int[,,,,]))]
        [InlineData(typeof(string))]
        public void LoadReflectionGenericType(Type t)
        {
            var ref1 = TypeReference.FromType(t);
            var ilspyType = cx.GetTypeReference(TypeReference.FromType(t));
            var ref2 = SymbolLoader.TypeRef(ilspyType);
            Assert.Equal(ref1, ref2);
        }

        [Fact]
        public void LoadReflectionGenericMethod()
        {
            var ref1 = MethodReference.FromLambda(() => Array.Empty<String>());
            var ilspyMethod = cx.GetMethod(ref1);
            Assert.Equal("System.Array.Empty", ilspyMethod.ReflectionName);
            Assert.Equal("System.String", ilspyMethod.TypeArguments.Single().ReflectionName);
            var ref2 = SymbolLoader.Method(ilspyMethod);
            Assert.Equal(ref1.Signature, ref2);
        }

        [Fact]
        public void LoadReflectionGenericMethod2()
        {
            var ref1 = MethodReference.FromLambda(() => Array.AsReadOnly<string>(default));
            var ilspyMethod = cx.GetMethod(ref1);
            Assert.Equal("System.Array.AsReadOnly", ilspyMethod.ReflectionName);
            Assert.Equal("System.String", ilspyMethod.TypeArguments.Single().ReflectionName);
            var ref2 = SymbolLoader.Method(ilspyMethod);
            Assert.Equal(ref1.Signature, ref2);
        }

        [Theory]
        [InlineData(typeof(int[]))]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(List<int[,,,,,]>))]
        [InlineData(typeof(Dictionary<string, List<int[,,,,,]>>))]
        [InlineData(typeof(int))]
        public void TypeFormatter(Type type)
        {
            var t2 = TypeReference.FromType(type);
            var t3 = cx.GetTypeReference(t2);
            var a = SymbolFormatter.TypeToString(type);
            var b = SymbolFormatter.TypeToString(t3);
            Assert.Equal(a, b);
        }

        [Theory]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(List<int[,,,,,]>))]
        [InlineData(typeof(Dictionary<string, List<int[,,,,,]>>))]
        [InlineData(typeof(int))]
        [InlineData(typeof(Array))]
        public void MethodFormatter(Type type)
        {
            foreach (var method_ in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                var method = method_;
                if (method.IsGenericMethodDefinition)
                    method = method.MakeGenericMethod(method.GetGenericArguments().Select(_ => typeof(string)).ToArray());
                var m2 = MethodReference.FromReflection(method);
                var m3 = cx.GetMethod(m2);
                var a = SymbolFormatter.MethodToString(method);
                var b = SymbolFormatter.MethodToString(m3);
                output.WriteLine($"Checking '{m2}', in ILSpy '{m3}', in test '{a}', ILSpy member def '{m3.MemberDefinition}'");
                Assert.Equal(a, b);
            }
        }
    }
}
