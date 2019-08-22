using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler.IL;
using TS=ICSharpCode.Decompiler.TypeSystem;
using Xunit;
using ICSharpCode.Decompiler.TypeSystem;
using Coberec.CSharpGen.TypeSystem;
using System.Collections.Immutable;
using Coberec.CSharpGen;

namespace Coberec.ExprCS.CodeTranslation
{
    partial class CodeTranslator
    {
        ILInstruction CallLocalFunction(ILFunction localFunction, IEnumerable<ILInstruction> arguments)
        {
            var reducedMethod = localFunction.ReducedMethod;
            Assert.NotNull(reducedMethod);

            // logic taken from LocalFunctionDecompiler.TransformToLocalFunctionInvocation, but it does not have to modify/parse the old "normal method" call
            var call = new Call(reducedMethod);
            call.Arguments.AddRange(arguments);
            // well, from what I look, we don't have to do anything...
            return call;
        }

        Result TranslateInvoke(InvokeExpression e)
        {
            var function = Assert.IsType<TypeReference.FunctionTypeCase>(e.Function.Type()).Item;

            var args = e.Args.Select(TranslateExpression).ToArray();

            if (e.Function is Expression.ParameterCase variable && this.ActiveLocalFunctions.TryGetValue(variable.Item.Id, out var localFunction))
            {
                var call = CallLocalFunction(localFunction, args.Select(a => new LdLoc(a.Output)));
                return Result.Concat(
                    args.Append(Result.Expression(localFunction.Method.ReturnType, call))
                );
            }
            else
            {
                var target = TranslateExpression(e.Function);
                var functionRealType = target.Output.Type;
                Assert.Equal(TS.TypeKind.Delegate, functionRealType.Kind);

                var invokeMethod = functionRealType.GetDelegateInvokeMethod();

                var call = new Call(invokeMethod);
                call.Arguments.Add(new LdLoc(target.Output));
                call.Arguments.AddRange(args.Select(a => new LdLoc(a.Output)));
                return Result.Concat(
                    args.Append(Result.Expression(invokeMethod.ReturnType, call))
                );
            }
        }

        Result TranslateLocalFunction(FunctionExpression function, ParameterExpression variable, Expression validIn)
        {
            var parameters = function.Params.Select(p => MetadataDefiner.CreateParameter(this.Metadata, p)).ToImmutableArray();
            var fakeName = $"<{this.GeneratedMethod.Name}>g__{variable.Name}|x_y";
            var method = new VirtualMethod(
                this.GeneratedMethod.DeclaringTypeDefinition,
                TS.Accessibility.Private,
                fakeName,
                parameters,
                Metadata.GetTypeReference(function.Body.Type()),
                isHidden: true
            );

            foreach (var (i, p) in function.Args.Indexed())
                this.Parameters.Add(p.Id, new ILVariable(VariableKind.Parameter, this.Metadata.GetTypeReference(p.Type), i) { Name = p.Name });
            var pendingFunctions = this.PendingLocalFunctions;
            this.PendingLocalFunctions = new List<ILFunction>();

            var bodyR = this.TranslateExpression(function.Body);
            var bodyC = this.BuildBContainer(bodyR);

            var fn = ILAstFactory.CreateFunction(method, bodyC, functionKind: ILFunctionKind.LocalFunction);
            fn.ReducedMethod = new TS.Implementation.LocalFunctionMethod(method, numberOfCompilerGeneratedParameters: 0);

            foreach (var p in function.Args)
                this.Parameters.Remove(p.Id);
            fn.LocalFunctions.AddRange(this.PendingLocalFunctions);
            this.PendingLocalFunctions = pendingFunctions;
            this.ActiveLocalFunctions.Add(variable.Id, fn);
            this.PendingLocalFunctions.Add(fn);

            Assert.Equal(method.ReturnType.GetStackType(), bodyC.ResultType);

            var result = TranslateExpression(validIn);

            Assert.True(this.ActiveLocalFunctions.Remove(variable.Id));

            return result;
        }

        Result TranslateFunction(FunctionExpression item)
        {
            throw new NotImplementedException();
        }

        Result TranslateFunctionConversion(FunctionConversionExpression item)
        {
            throw new NotImplementedException();
        }
    }
}