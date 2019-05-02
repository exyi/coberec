using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests
{
    public class MetadataDefinitionTests
    {
        OutputChecker check = new OutputChecker("testoutput");
        MetadataContext MkContext() => MetadataContext.Create("MyModule");
        [Fact]
        public void OneEmptyType()
        {
            var ns = new NamespaceSignature("MyNamespace", parent: null);
            var type = new TypeSignature(
                "MyType",
                ns,
                isSealed: false,
                isAbstract: false,
                Accessibility.APublic,
                0
            );
            var typeDef = new TypeDef(type, ImmutableArray<MemberDef>.Empty);

            var cx = MkContext();
            cx.AddType(typeDef);
            check.CheckString(cx.EmitToString());
        }

        [Fact]
        public void FewFields()
        {
            var cx = MkContext();

            var stringT = cx.FindType(typeof(string));
            var stringArr = cx.FindType(typeof(string[]));
            var someTuple = cx.FindType(typeof((List<int>, System.Threading.Tasks.Task)));

            var ns = new NamespaceSignature("MyNamespace", parent: null);
            var type = new TypeSignature(
                "MyType",
                ns,
                isSealed: false,
                isAbstract: false,
                Accessibility.APublic,
                0
            );
            var typeDef = new TypeDef(type, ImmutableArray.Create<MemberDef>(
                new FieldDef(new FieldSignature(type, "F1", Accessibility.APublic, stringT, false, true)),
                new FieldDef(new FieldSignature(type, "F2", Accessibility.APrivate, stringArr, false, true)),
                new FieldDef(new FieldSignature(type, "F3", Accessibility.AInternal, someTuple, false, true)),
                new FieldDef(new FieldSignature(type, "F4", Accessibility.AProtectedInternal, stringArr, true, false))
            ));

            cx.AddType(typeDef);
            check.CheckString(cx.EmitToString());
        }
    }
}
