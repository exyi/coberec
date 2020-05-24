using System;
using CheckTestOutput;
using Xunit;

namespace Coberec.ExprCS.Tests.Docs
{
    public class E2E
    {
        OutputChecker check = new OutputChecker("testoutput");
        [Fact]
        public void HelloWorld()
        {

            // declare
            // namespace MyApp.HelloWorld {
            var ns = NamespaceSignature.Parse("MyApp.HelloWorld");
            // public class Program {
            var programType = TypeSignature.Class("Program", ns, Accessibility.APublic);
            // public static int Main() {
            var mainMethod = MethodSignature.Static("Main", programType, Accessibility.APublic, returnType: TypeSignature.Int32);

            // get the Console.WriteLine reference
            var writeLineRef = MethodReference.FromLambda(() => Console.WriteLine(""));

            var body = new [] {
                Expression.StaticMethodCall(writeLineRef, Expression.Constant("Hello world!"))
            }.ToBlock(
                result: Expression.Constant(0)
            );

            var type = TypeDef.Empty(programType).AddMember(
                MethodDef.Create(mainMethod, body)
            );

            var cx = MetadataContext.Create();
            cx.AddType(type);
            var csharp = cx.EmitToString();

            check.CheckString(csharp, fileExtension: "cs");
        }
    }
}
