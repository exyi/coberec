using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using TrainedMonkey.CSharpGen.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;

namespace TrainedMonkey.CSharpGen.Emit
{
    public static class EmitExtensions
    {
        // formats for generated members:
        public const string AutoPropertyField = "<{0}>k__BackingField";
        public const string PropertyGetter = "get_{0}";
        public const string PropertySetter = "set_{0}";

        public static (IProperty, IField) AddAutoProperty(this VirtualType declaringType, string name, IType propertyType, Accessibility accessibility = Accessibility.Public, bool isReadOnly = true)
        {
            name = SymbolNamer.NameMember(declaringType, name, lowerCase: accessibility == Accessibility.Private);


            var field = new VirtualField(declaringType, Accessibility.Private, string.Format(AutoPropertyField, name), propertyType, isReadOnly: isReadOnly, isHidden: true);
            field.Attributes.Add(declaringType.Compilation.CompilerGeneratedAttribute());

            var getter = new VirtualMethod(declaringType, accessibility, string.Format(PropertyGetter, name), Array.Empty<IParameter>(), propertyType, isHidden: true);
            getter.BodyFactory = () => CreateExpressionFunction(getter,
                new IL.LdObj(new IL.LdFlda(new IL.LdLoc(new IL.ILVariable(IL.VariableKind.Parameter, declaringType, -1)), field), propertyType)
            );
            getter.Attributes.Add(declaringType.Compilation.CompilerGeneratedAttribute());
            var setter = isReadOnly ? null :
                         new VirtualMethod(declaringType, accessibility, string.Format(PropertySetter, name), new [] { new DefaultParameter(propertyType, "value") }, declaringType.Compilation.FindType(typeof(void)), isHidden: true);

            var prop = new VirtualProperty(declaringType, accessibility, name, getter, setter);

            declaringType.Methods.Add(getter);
            if (setter != null) declaringType.Methods.Add(setter);

            declaringType.Fields.Add(field);
            declaringType.Properties.Add(prop);

            return (prop, field);
        }

        public static IL.ILFunction CreateOneBlockFunction(IMethod method, params IL.ILInstruction[] instructions)
        {
            var isVoid = method.ReturnType.FullName == "System.Void";

            var functionContainer = new IL.BlockContainer(expectedResultType: isVoid ? IL.StackType.Void : instructions.Last().ResultType);
            var block = new IL.Block();
            var variables = new VariableCollectingVisitor();
            foreach (var i in instructions)
            {
                i.AcceptVisitor(variables);
                if (i == instructions.Last() && !isVoid)
                {
                    block.Instructions.Add(new IL.Leave(functionContainer, value: i));
                }
                else
                    block.Instructions.Add(i);
            }
            if (isVoid) block.Instructions.Add(new IL.Leave(functionContainer));

            functionContainer.Blocks.Add(block);

            var ilFunc = new IL.ILFunction(method, 10000, new ICSharpCode.Decompiler.TypeSystem.GenericContext(), functionContainer);

            foreach (var i in variables.Variables)
            {
                if (i.Function == null)
                    ilFunc.Variables.Add(i);
            }

            ilFunc.AddRef(); // whatever, somehow initializes the freaking tree
            ilFunc.CheckInvariantPublic(IL.ILPhase.Normal);
            return ilFunc;
        }

        public static IL.ILFunction CreateExpressionFunction(IMethod method, IL.ILInstruction root) =>
            CreateOneBlockFunction(method, root);

        class VariableCollectingVisitor : IL.ILVisitor
        {
            public readonly HashSet<IL.ILVariable> Variables = new HashSet<IL.ILVariable>();

            protected override void Default(IL.ILInstruction inst)
            {
                if (inst is IL.IInstructionWithVariableOperand lfslfd)
                    Variables.Add(lfslfd.Variable);

                foreach(var c in inst.Children)
                    c.AcceptVisitor(this);
            }
        }

        public static IAttribute CompilerGeneratedAttribute(this ICompilation compilation) =>
            new DefaultAttribute(
                KnownAttributes.FindType(compilation, KnownAttribute.CompilerGenerated),
                ImmutableArray<CustomAttributeTypedArgument<IType>>.Empty,
                ImmutableArray<CustomAttributeNamedArgument<IType>>.Empty
            );
    }
}
