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
                var call = CallLocalFunction(localFunction, args.Select(a => a.Instr()));
                return Result.Concat(
                    args.Append(Result.Expression(localFunction.Method.ReturnType, call))
                );
            }
            else
            {
                var target = TranslateExpression(e.Function);
                var functionRealType = target.Type;
                Assert.Equal(TS.TypeKind.Delegate, functionRealType.Kind);

                var invokeMethod = functionRealType.GetDelegateInvokeMethod();

                var call = new Call(invokeMethod);
                call.Arguments.Add(target.Instr());
                call.Arguments.AddRange(args.Select(a => a.Instr()));
                return Result.Concat(
                    args.Append(Result.Expression(invokeMethod.ReturnType, call))
                        .Prepend(target)
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

            var fn = ILAstFactory.CreateFunction(method, bodyC, function.Args.Select(a => this.Parameters[a.Id]), functionKind: ILFunctionKind.LocalFunction);
            fn.ReducedMethod = new TS.Implementation.LocalFunctionMethod(method, numberOfCompilerGeneratedParameters: 0, numberOfCompilerGeneratedTypeParameters: 0);

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

        Result TranslateFunction(FunctionExpression e) => TranslateFunction(e, TypeReference.FunctionType(e.Params, e.Body.Type()));
        Result TranslateFunction(FunctionExpression e, TypeReference expectedType)
        {
            var delegateType =
                expectedType is TypeReference.FunctionTypeCase fnType ? this.FindAppropriateDelegate(fnType.Item) :
                this.Metadata.GetTypeReference(expectedType);

            Assert.True(delegateType.GetDelegateInvokeMethod() is object, $"Can not create a lambda of type {delegateType} as it's not a delegate.");

            var parameters = e.Params.EagerSelect(p => MetadataDefiner.CreateParameter(this.Metadata, p));
            var fakeName = $"<{this.GeneratedMethod.Name}>somethingsomething";
            var method = new VirtualMethod(
                this.GeneratedMethod.DeclaringTypeDefinition,
                TS.Accessibility.Private,
                fakeName,
                parameters,
                delegateType.GetDelegateInvokeMethod().ReturnType,
                isHidden: true
            );

            foreach (var (i, p) in e.Args.Indexed())
                this.Parameters.Add(p.Id, new ILVariable(VariableKind.Parameter, this.Metadata.GetTypeReference(p.Type), i) { Name = p.Name });
            var pendingFunctions = this.PendingLocalFunctions;
            this.PendingLocalFunctions = new List<ILFunction>();

            var bodyR = this.TranslateExpression(e.Body);
            var bodyC = this.BuildBContainer(bodyR);

            var fn = ILAstFactory.CreateFunction(method, bodyC, e.Args.Select(a => this.Parameters[a.Id]), functionKind: ILFunctionKind.Delegate);
            fn.DelegateType = delegateType;

            foreach (var p in e.Args)
                this.Parameters.Remove(p.Id);
            fn.LocalFunctions.AddRange(this.PendingLocalFunctions);
            this.PendingLocalFunctions = pendingFunctions;

            Assert.Equal(e.Params.Length, fn.Variables.Count(v => v.Kind == VariableKind.Parameter));

            return Result.Expression(delegateType, fn);
        }

        Result TranslateFunctionConversion(FunctionConversionExpression e)
        {
            if (e.Value is Expression.FunctionCase fn)
                return this.TranslateFunction(fn.Item, e.Type);

            var fnType = e.Type as TypeReference.FunctionTypeCase;
            var to = fnType is object ? this.FindAppropriateDelegate(fnType.Item) :
                     this.Metadata.GetTypeReference(e.Type);
            var target = this.TranslateExpression(e.Value);

            var invokeMethod = target.Type.GetDelegateInvokeMethod();
            Assert.NotNull(invokeMethod);
            var fromFnType = new FunctionType(invokeMethod.Parameters.Select(SymbolLoader.Parameter).ToImmutableArray(), SymbolLoader.TypeRef(invokeMethod.ReturnType));
            // if the result is function compatible with the delegate -> return it
            if (fnType is object && fnType == fromFnType)
            {
                return target;
            }

            // attempt standard reference conversion
            var conversion = this.Metadata.CSharpConversions.ExplicitConversion(target.Type, to);
            if (conversion.IsIdentityConversion)
                return target;
            if (conversion.IsReferenceConversion)
                return Result.Concat(
                    target,
                    Result.Expression(to, target.Instr())
                );

            // reference conversion did not work? lets just create a lambda that will invoke the old function


            var targetVar = ParameterExpression.Create(e.Value.Type(), "convertedFunction");
            var ilVar = new ILVariable(VariableKind.StackSlot, target.Type);
            // hack: we add the function to a temporary function so the lambda does not take ownership of it.
            new ILFunction(null, 10000, new ICSharpCode.Decompiler.TypeSystem.GenericContext(), new BlockContainer(), ILFunctionKind.Delegate).Variables.Add(ilVar);
            this.Parameters.Add(targetVar.Id, ilVar);
            var args = fromFnType.Params.EagerSelect(p => ParameterExpression.Create(p.Type, p.Name));
            var newFunction = TranslateFunction(new FunctionExpression(fromFnType.Params, args, targetVar.Read().FunctionConvert(fromFnType).Invoke(args.EagerSelect(a => a.Read()))));
            this.Parameters.Remove(targetVar.Id);
            // here, we remove the temporary hack function from ilVar
            ilVar.Function.Variables.Remove(ilVar);
            return Result.Concat(
                target,
                new Result(new ExpressionStatement(new StLoc(ilVar, target.Instr()))),
                newFunction
            );
        }

        IType FindAppropriateDelegate(FunctionType type)
        {
            return this.Metadata.GetTypeReference(
                type.TryGetDelegate() ?? throw new NotSupportedException($"Could not translate {type} into a delegate")
            );
        }
    }
}
