using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.CSharpGen.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;

namespace Coberec.ExprCS
{
    public class ILSpyMethodBody : Expression
    {
        public Func<IMethod, MetadataContext, ILFunction> BuildBody { get; }

        public override TResult Match<TResult>(Func<BinaryCase, TResult> binary, Func<NotCase, TResult> not, Func<MethodCallCase, TResult> methodCall, Func<NewObjectCase, TResult> newObject, Func<FieldAccessCase, TResult> fieldAccess, Func<ReferenceAssignCase, TResult> referenceAssign, Func<DereferenceCase, TResult> dereference, Func<VariableReferenceCase, TResult> variableReference, Func<AddressOfCase, TResult> addressOf, Func<NumericConversionCase, TResult> numericConversion, Func<ReferenceConversionCase, TResult> referenceConversion, Func<ConstantCase, TResult> constant, Func<DefaultCase, TResult> @default, Func<ParameterCase, TResult> parameter, Func<ConditionalCase, TResult> conditional, Func<FunctionCase, TResult> function, Func<FunctionConversionCase, TResult> functionConversion, Func<InvokeCase, TResult> invoke, Func<BreakCase, TResult> @break, Func<BreakableCase, TResult> breakable, Func<LoopCase, TResult> loop, Func<LetInCase, TResult> letIn, Func<BlockCase, TResult> block, Func<LowerableCase, TResult> lowerable)
        {
            throw new NotSupportedException();
        }

        private protected override bool EqualsCore(Expression b)
        {
            return object.ReferenceEquals(this, b);
        }

        public ILSpyMethodBody(Func<IMethod, MetadataContext, ILFunction> buildBody)
        {
            BuildBody = buildBody;
        }
    }

    public class ILSpyArbitraryTypeModification
    {
        public Action<VirtualType> DeclareMembers { get; }
        public Action<VirtualType> CompleteDefinitions { get; }

        public ILSpyArbitraryTypeModification(Action<VirtualType> declareMembers, Action<VirtualType> completeDefinitions)
        {
            DeclareMembers = declareMembers ?? throw new ArgumentNullException(nameof(declareMembers));
            CompleteDefinitions = completeDefinitions;
        }
    }
}
