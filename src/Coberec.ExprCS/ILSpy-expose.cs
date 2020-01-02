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

        public override T Match<T>(Func<BinaryExpression, T> binary, Func<NotExpression, T> not, Func<MethodCallExpression, T> methodCall, Func<NewObjectExpression, T> newObject, Func<FieldAccessExpression, T> fieldAccess, Func<ReferenceAssignExpression, T> referenceAssign, Func<DereferenceExpression, T> dereference, Func<VariableReferenceExpression, T> variableReference, Func<AddressOfExpression, T> addressOf, Func<NumericConversionExpression, T> numericConversion, Func<ReferenceConversionExpression, T> referenceConversion, Func<ConstantExpression, T> constant, Func<DefaultExpression, T> @default, Func<ParameterExpression, T> parameter, Func<ConditionalExpression, T> conditional, Func<FunctionExpression, T> function, Func<FunctionConversionExpression, T> functionConversion, Func<InvokeExpression, T> invoke, Func<BreakExpression, T> @break, Func<BreakableExpression, T> breakable, Func<LoopExpression, T> loop, Func<LetInExpression, T> letIn, Func<NewArrayExpression, T> newArray, Func<ArrayIndexExpression, T> arrayIndex, Func<BlockExpression, T> block, Func<LowerableExpression, T> lowerable)
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
