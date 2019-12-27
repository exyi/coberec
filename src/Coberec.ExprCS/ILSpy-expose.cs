using System;
using System.Collections.Generic;
using System.Linq;
using Coberec.CSharpGen.TypeSystem;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;

namespace Coberec.ExprCS
{
    /// <summary> ILSpy expose: Special expression that allow you to create any body using the ILSpy internal tree. Must be directly in the method body. </summary>
    public class ILSpyMethodBody : Expression
    {
        public Func<IMethod, MetadataContext, ILFunction> BuildBody { get; }

        private protected override bool EqualsCore(Expression b)
        {
            return object.ReferenceEquals(this, b);
        }

        public override TResult Match<TResult>(Func<BinaryCase, TResult> binary, Func<NotCase, TResult> not, Func<MethodCallCase, TResult> methodCall, Func<NewObjectCase, TResult> newObject, Func<FieldAccessCase, TResult> fieldAccess, Func<ReferenceAssignCase, TResult> referenceAssign, Func<DereferenceCase, TResult> dereference, Func<VariableReferenceCase, TResult> variableReference, Func<AddressOfCase, TResult> addressOf, Func<NumericConversionCase, TResult> numericConversion, Func<ReferenceConversionCase, TResult> referenceConversion, Func<ConstantCase, TResult> constant, Func<DefaultCase, TResult> @default, Func<ParameterCase, TResult> parameter, Func<ConditionalCase, TResult> conditional, Func<FunctionCase, TResult> function, Func<FunctionConversionCase, TResult> functionConversion, Func<InvokeCase, TResult> invoke, Func<BreakCase, TResult> @break, Func<BreakableCase, TResult> breakable, Func<LoopCase, TResult> loop, Func<LetInCase, TResult> letIn, Func<NewArrayCase, TResult> newArray, Func<ArrayIndexCase, TResult> arrayIndex, Func<BlockCase, TResult> block, Func<LowerableCase, TResult> lowerable)
        {
            throw new NotImplementedException();
        }

        public ILSpyMethodBody(Func<IMethod, MetadataContext, ILFunction> buildBody)
        {
            BuildBody = buildBody;
        }
    }

    /// <summary> ILSpy expose: represents any function that may modify the internal ILSpy class when it's created </summary>
    public class ILSpyArbitraryTypeModification
    {
        /// <summary> Called when the class members should be declared. The standard members are not declared at this point. </summary>
        public Action<VirtualType> DeclareMembers { get; }
        /// <summary> Called when the declard members should be filled with method bodies and so on. All members are declared at this point and can be referenced. </summary>
        public Action<VirtualType> CompleteDefinitions { get; }

        public ILSpyArbitraryTypeModification(Action<VirtualType> declareMembers, Action<VirtualType> completeDefinitions)
        {
            DeclareMembers = declareMembers ?? throw new ArgumentNullException(nameof(declareMembers));
            CompleteDefinitions = completeDefinitions;
        }
    }
}
