using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.TypeSystem;
using Xunit;

namespace Coberec.ExprCS.CodeTranslation
{
    static class ILAstFactory
    {
        public static ILInstruction Constant(object constant, IType type)
        {
            if (constant == null)
            {
                Assert.True(type.IsReferenceType);
                return new LdNull();
            }
            else if (constant is bool boolC)
            {
                Assert.Equal(typeof(bool).FullName, type.FullName);
                return new LdcI4(boolC ? 1 : 0);
            }
            else if (constant is char || constant is byte || constant is sbyte || constant is ushort || constant is short || constant is int)
            {
                return new LdcI4(Convert.ToInt32(constant));
            }
            else if (constant is uint uintC)
            {
                return new LdcI4(unchecked((int)uintC));
            }
            else if (constant is long longC)
                return new LdcI8(longC);
            else if (constant is ulong ulongC)
                return new LdcI8(unchecked((long)ulongC));
            else if (constant is float floatC)
                return new LdcF4(floatC);
            else if (constant is double doubleC)
                return new LdcF8(doubleC);
            else if (constant is decimal decimalC)
                return new LdcDecimal(decimalC);
            else if (constant is string stringC)
                return new LdStr(stringC);
            else
                throw new NotSupportedException($"Constant '{constant}' of type '{constant.GetType()}' with declared type '{type}' is not supported.");
        }

        public static ILInstruction FieldAddr(IField field, ILVariable target) =>
            target is object ?
            new LdFlda(new LdLoc(target), field) :
            (ILInstruction)new LdsFlda(field);

        public static ILFunction CreateFunction(IMethod method, BlockContainer functionContainer, IEnumerable<ILVariable> morevariables = null, ILFunctionKind functionKind = ILFunctionKind.TopLevelFunction)
        {
            Assert.Equal(method.ReturnType.GetStackType(), functionContainer.ExpectedResultType);

            var variables = new VariableCollectingVisitor();
            // r.Output?.Apply(variables.Variables.Add);
            if (morevariables is object) foreach (var p in morevariables)
                variables.Variables.Add(p);

            functionContainer.AcceptVisitor(variables);

            var ilFunc = new ILFunction(method, 10000, new ICSharpCode.Decompiler.TypeSystem.GenericContext(), functionContainer, functionKind);

            foreach (var i in variables.Variables)
                if (i.Function == null)
                    ilFunc.Variables.Add(i);

            return ilFunc;
        }

        class VariableCollectingVisitor : ILVisitor
        {
            public readonly HashSet<ILVariable> Variables = new HashSet<ILVariable>();

            protected override void Default(ILInstruction inst)
            {
                if (inst is IInstructionWithVariableOperand a)
                    Variables.Add(a.Variable);

                foreach(var c in inst.Children)
                    c.AcceptVisitor(this);
            }
        }
    }
}
