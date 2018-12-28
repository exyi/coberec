using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp.Resolver;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using Coberec.CSharpGen.TypeSystem;
using IL=ICSharpCode.Decompiler.IL;
using Coberec.CoreLib;

namespace Coberec.CSharpGen.Emit
{
    public static class EmitExtensions
    {
        // formats for generated members:
        public const string AutoPropertyField = "<{0}>k__BackingField";
        public const string PropertyGetter = "get_{0}";
        public const string PropertySetter = "set_{0}";

        public static IProperty AddExplicitInterfaceProperty(this VirtualType declaringType, IProperty ifcProperty, IMember baseMember)
        {
            Debug.Assert(declaringType.Equals(baseMember.DeclaringType) || declaringType.GetAllBaseTypeDefinitions().Contains(baseMember.DeclaringType));
            Debug.Assert(ifcProperty.DeclaringType.Kind == TypeKind.Interface);

            var getter = new VirtualMethod(declaringType, Accessibility.Private, "get_" + ifcProperty.FullName, new IParameter[0], ifcProperty.ReturnType, explicitImplementations: new [] { ifcProperty.Getter }, isHidden: true);
            getter.BodyFactory = () => {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, declaringType, -1);
                return CreateExpressionFunction(getter, new IL.LdLoc(thisParam).AccessMember(baseMember));
            };
            var setter =
                ifcProperty.Setter == null ? null :
                new VirtualMethod(declaringType, Accessibility.Private, "get_" + ifcProperty.FullName, ifcProperty.Setter.Parameters, ifcProperty.Setter.ReturnType, explicitImplementations: new [] { ifcProperty.Setter }, isHidden: true);

            if (setter != null)
                setter.BodyFactory = () => {
                    var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, declaringType, -1);
                    var valueParam = new IL.ILVariable(IL.VariableKind.Parameter, declaringType, 0);
                    return CreateExpressionFunction(getter, new IL.LdLoc(thisParam).AssignMember(baseMember, new IL.LdLoc(valueParam)));
                };

            var prop = new VirtualProperty(declaringType, Accessibility.Private, ifcProperty.FullName, getter, setter, explicitImplementations: new [] { ifcProperty });

            getter.ApplyAction(declaringType.Methods.Add);
            setter?.ApplyAction(declaringType.Methods.Add);
            prop.ApplyAction(declaringType.Properties.Add);

            return prop;
        }

        public static (IProperty prop, IField field) AddAutoProperty(this VirtualType declaringType, string name, IType propertyType, Accessibility accessibility = Accessibility.Public, bool isReadOnly = true)
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

        public static IProperty AddInterfaceProperty(this VirtualType declaringType, string name, IType propertyType, bool isReadOnly = true)
        {
            name = SymbolNamer.NameMember(declaringType, name, lowerCase: false);

            var getter = new VirtualMethod(declaringType, Accessibility.Public, string.Format(PropertyGetter, name), Array.Empty<IParameter>(), propertyType, isHidden: true);
            var setter = isReadOnly ? null :
                         new VirtualMethod(declaringType, Accessibility.Public, string.Format(PropertySetter, name), new [] { new DefaultParameter(propertyType, "value") }, declaringType.Compilation.FindType(typeof(void)), isHidden: true);
            var prop = new VirtualProperty(declaringType, Accessibility.Public, name, getter, setter, isVirtual: true);


            declaringType.Methods.Add(getter);
            if (setter != null) declaringType.Methods.Add(setter);
            declaringType.Properties.Add(prop);
            return prop;
        }

        public static IL.ILInstruction InvokeInterfaceMethod(IMethod method, IType targetType, IL.ILInstruction @this, params IL.ILInstruction[] args)
        {
            var explicitImplementation = targetType.GetMethods().FirstOrDefault(m => m.ExplicitlyImplementedInterfaceMembers.Contains(method));
            // var implicitImplementation = propertyType.GetMethods().FirstOrDefault(m => m.
            var usedMethod = explicitImplementation?.Accessibility == Accessibility.Public ? explicitImplementation : method;
            // TODO: call the method directly if there is some
            var call = new IL.CallVirt(usedMethod);
            if ((bool)targetType.IsReferenceType)
                call.Arguments.Add(@this);
            else
            {
                call.ConstrainedTo = targetType;
                call.Arguments.Add(new IL.AddressOf(@this));
            }
            call.Arguments.AddRange(args);
            return call;
        }

        public static IL.ILInstruction AndAlso(params IL.ILInstruction[] clauses) => AndAlso(clauses.AsEnumerable());
        public static IL.ILInstruction AndAlso(IEnumerable<IL.ILInstruction> clauses)
        {
            if (clauses.Any())
                return clauses.Aggregate((a, b) => new IL.IfInstruction(a, b, new IL.LdcI4(0)));
            else
                return new IL.LdcI4(1);
        }

        public static IType GetObjectResultType(this IL.ILInstruction instruction)
        {
            // if (instruction.ResultType != IL.StackType.O && instruction.ResultType != IL.StackType.Ref)
            //     throw new InvalidOperationException($"Can't get result type of non-object expression.");

            switch(instruction)
            {
                case IL.Call call: return call.Method.ReturnType;
                case IL.CallVirt callv: return callv.Method.ReturnType;
                case IL.NewObj newobj: return newobj.Method.DeclaringType;
                case IL.IInstructionWithFieldOperand field : return field.Field.ReturnType;
                case IL.ILoadInstruction load: return load.Variable.Type;
                case IL.IAddressInstruction addrLoad: return addrLoad.Variable.Type;
                case IL.AddressOf addrOf: return addrOf.Value.GetObjectResultType();
                case IL.LdObj ldobj: return ldobj.Target.GetObjectResultType();
                case IL.Box box: return box.Type;

                default: throw new NotSupportedException($"Can't get result type from instruction {instruction.GetType()}");
            }
        }

        public static IL.ILInstruction CreateTuple(ICompilation compilation, params IL.ILInstruction[] nodes)
        {
            var restTuple = typeof(ValueTuple<,,,,,,,>);
            Debug.Assert(restTuple.GetGenericArguments().Last().Name == "TRest");
            var maxSize = restTuple.GetGenericArguments().Length;
            if (nodes.Length >= maxSize)
                return makeTuple(nodes.Take(maxSize - 1).Append(CreateTuple(compilation, nodes.Skip(maxSize - 1).ToArray())).ToArray());
            else
                return makeTuple(nodes);

            IL.ILInstruction makeTuple(IL.ILInstruction[] n)
            {
                var t = //n.Length == 0 ? typeof(ValueTuple) :
                        n.Length == 1 ? typeof(ValueTuple<>) :
                        n.Length == 2 ? typeof(ValueTuple<,>) :
                        n.Length == 3 ? typeof(ValueTuple<,,>) :
                        n.Length == 4 ? typeof(ValueTuple<,,,>) :
                        n.Length == 5 ? typeof(ValueTuple<,,,,>) :
                        n.Length == 6 ? typeof(ValueTuple<,,,,,>) :
                        n.Length == 7 ? typeof(ValueTuple<,,,,,,>) :
                        n.Length == 8 ? typeof(ValueTuple<,,,,,,,>) :
                        throw new NotSupportedException($"ValueTuple can not have {n.Length} parameters");
                var tt = new ParameterizedType(compilation.FindType(t), n.Select(a => a.GetObjectResultType()));
                var ctor = tt.GetConstructors().Single(c => c.Parameters.Count == n.Length);
                var result = new IL.NewObj(ctor);
                result.Arguments.AddRange(n);
                return result;
            }
        }

        public static IL.ILInstruction AccessMember(this IL.ILInstruction target, IMember p) =>
            p is IProperty property ? new IL.Call(property.Getter) { Arguments = { target } } :
            p is IField field ? (IL.ILInstruction)new IL.LdObj(new IL.LdFlda(target, field), field.ReturnType) :
            throw new NotSupportedException($"{p.GetType()}");

        public static IL.ILInstruction AssignMember(this IL.ILInstruction target, IMember p, IL.ILInstruction value) =>
            p is IProperty property ? new IL.Call(property.Setter) { Arguments = { target, value } } :
            p is IField field ? (IL.ILInstruction)new IL.StObj(new IL.LdFlda(target, field), value, field.ReturnType) :
            throw new NotSupportedException($"{p.GetType()}");

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
                if (i.Function == ilFunc && i.Kind == IL.VariableKind.Parameter)
                {
                    if (i.Index == -1)
                        i.Name = "this";
                    else
                        i.Name = method.Parameters[i.Index].Name;
                }
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

        public static IL.ILInstruction MakeArray(IType elementType, IL.ILInstruction[] items)
        {
            var variable = new IL.ILVariable(IL.VariableKind.InitializerTarget, new ArrayType(elementType.GetDefinition().Compilation, elementType), 0);
            var block = new IL.Block(IL.BlockKind.ArrayInitializer);
            block.Instructions.Add(new IL.StLoc(variable, new IL.NewArr(elementType, new IL.LdcI4(items.Length))));
            foreach (var (index, item) in items.Indexed())
            {
                block.Instructions.Add(new IL.StObj(
                    new IL.LdElema(elementType, new IL.LdLoc(variable), new IL.LdcI4(index)),
                    item,
                    elementType
                ));
            }
            block.FinalInstruction = new IL.LdLoc(variable);
            return block;
        }
    }
}
