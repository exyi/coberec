using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp.Resolver;
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

        public static IMethod AddCreateConstructor(this VirtualType type, EmitContext cx, (string name, IField field)[] fields)
        {
            var parameters = SymbolNamer.NameParameters(fields.Select(f => (IParameter)new DefaultParameter(f.field.Type, f.name))); // TODO: the name is not sanitized, I just want to see the tests finding this bug ;)
            var ctor = new VirtualMethod(type, Accessibility.Public, ".ctor", parameters, cx.FindType(typeof(void)));
            var objectCtor = cx.FindMethod(() => new object());
            ctor.BodyFactory = () =>
            {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                return EmitExtensions.CreateOneBlockFunction(ctor,
                    fields.Zip(parameters, (f, p) =>
                    {
                        var index = Array.IndexOf(parameters, p);
                        return (IL.ILInstruction)new IL.StObj(new IL.LdFlda(new IL.LdLoc(thisParam), f.field), new IL.LdLoc(new IL.ILVariable(IL.VariableKind.Parameter, f.field.ReturnType, index) { Name = p.Name }), f.field.ReturnType);
                    })
                    .Prepend(new IL.Call(objectCtor) { Arguments = { new IL.LdLoc(thisParam) } })
                    .ToArray()
                );
            };

            type.Methods.Add(ctor);
            return ctor;
        }

        static IL.ILInstruction InvokeInterfaceMethod(IMethod method, IType targetType, IL.ILInstruction @this, params IL.ILInstruction[] args)
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

        public static IL.ILInstruction EqualsExpression(IType propertyType, IL.ILInstruction @this, IL.ILInstruction other)
        {
            var originalPropertyType = propertyType;
            bool needsBoxing;
            (propertyType, needsBoxing) =
                propertyType.FullName == "System.Nullable" ?
                (((ParameterizedType)propertyType).TypeArguments.Single(), true) :
                (propertyType, false);

            var eqInterface = propertyType.GetAllBaseTypes().FirstOrDefault(t => t.FullName == "System.IEquatable") as IType;
            var seqInterface = propertyType.GetAllBaseTypes().FirstOrDefault(t => t.FullName == "System.Collections.IStructuralEquatable") as IType;
            var enumerableInterface = propertyType.GetAllBaseTypes().FirstOrDefault(t => t.FullName == "System.Collections.IEnumerable") as IType;
            var eqOperator = propertyType.GetMethods(options: GetMemberOptions.IgnoreInheritedMembers).FirstOrDefault(t => t.IsOperator && t.Name == "op_Equality");
            if (propertyType.GetStackType() != IL.StackType.O)
            {
                return new IL.Comp(IL.ComparisonKind.Equality, needsBoxing ? IL.ComparisonLiftingKind.CSharp : IL.ComparisonLiftingKind.None, propertyType.GetStackType(), Sign.None, @this, other);
            }
            // enumerable types tend to be reference-equality even though they have IStructuralEquality overridden
            else if (seqInterface != null && (enumerableInterface != null || eqInterface == null))
            {
                return InvokeInterfaceMethod(propertyType.GetDefinition().Compilation.FindType(typeof(System.Collections.IEqualityComparer)).GetMethods(options: GetMemberOptions.IgnoreInheritedMembers).Single(m => m.Name == "Equals"), seqInterface,
                    new IL.CallVirt(propertyType.GetDefinition().Compilation.FindType(typeof(System.Collections.StructuralComparisons)).GetProperties().Single(p => p.Name == "StructuralEqualityComparer").Getter),
                    new IL.Box(@this, originalPropertyType),
                    new IL.Box(other, originalPropertyType)
                );
            }
            else if (eqOperator != null)
            {
                if (needsBoxing)
                    eqOperator = CSharpOperators.LiftUserDefinedOperator(eqOperator);
                return new IL.Call(eqOperator) { Arguments = { @this, other } };
            }
            else if (eqInterface != null && !needsBoxing)
            {
                return InvokeInterfaceMethod(
                    eqInterface.GetMethods(options: GetMemberOptions.IgnoreInheritedMembers).Single(m => m.Name == "Equals"),
                    propertyType,
                    @this,
                    other
                );
            }
            else
                throw new NotSupportedException($"Can't compare {originalPropertyType.ReflectionName}, it does not implement IEquatable.");
        }

        static IL.ILInstruction GetHashCodeExpression(IType propertyType, IL.ILInstruction prop)
        {
            var eqInterface = propertyType.GetAllBaseTypes().FirstOrDefault(t => t.FullName == "System.IEquatable") as IType;
            var seqInterface = propertyType.GetAllBaseTypes().FirstOrDefault(t => t.FullName == "System.Collections.IStructuralEquatable") as IType;
            var enumerableInterface = propertyType.GetAllBaseTypes().FirstOrDefault(t => t.FullName == "System.Collections.IEnumerable") as IType;
            var eqOperator = propertyType.GetMethods(options: GetMemberOptions.IgnoreInheritedMembers).FirstOrDefault(t => t.IsOperator && t.Name == "op_Equality");

            // enumerable types tend to be reference-equality even though they have IStructuralEquality overridden
            if (seqInterface != null && (enumerableInterface != null || eqInterface == null))
            {
                return InvokeInterfaceMethod(propertyType.GetDefinition().Compilation.FindType(typeof(System.Collections.IEqualityComparer)).GetMethods(options: GetMemberOptions.IgnoreInheritedMembers).Single(m => m.Name == "GetHashCode"), seqInterface,
                    new IL.CallVirt(propertyType.GetDefinition().Compilation.FindType(typeof(System.Collections.StructuralComparisons)).GetProperties().Single(p => p.Name == "StructuralEqualityComparer").Getter),
                    new IL.Box(prop, propertyType)
                );
            }
            else
            {
                // var method = propertyType.GetDefinition().Compilation.FindType(typeof(object)).GetMethods().Single(m => m.Name == "GetHashCode");
                return prop;
            }
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

        static IL.ILInstruction CreateTuple(ICompilation compilation, params IL.ILInstruction[] nodes)
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

        static IL.ILInstruction CombineHashCodes(ICompilation compilation, int? seed, params IL.ILInstruction[] nodes)
        {
            IL.ILInstruction invokeGetHashCode(IL.ILInstruction n)
            {
                if (n.ResultType == IL.StackType.I4) // integers return themselves anyway...
                    return n;
                else
                    return new IL.CallVirt(compilation.FindType(typeof(object)).GetMethods().Single(m => m.Name == "GetHashCode")) { Arguments = { n } };
            }
            if (nodes.Length == 0)
                return new IL.LdcI4(seed ?? 42);
            else if (nodes.Length == 1 && seed == null)
                return invokeGetHashCode(nodes[0]);
            else if (nodes.Length == 1)
                return invokeGetHashCode(new IL.BinaryNumericInstruction(IL.BinaryNumericOperator.Add, nodes[0], new IL.LdcI4((int)seed), false, Sign.Signed));
            else
            {
                if (seed != null)
                    nodes = nodes.Prepend(new IL.LdcI4((int)seed)).ToArray();
                var tuple = CreateTuple(compilation, nodes);
                var tupleType = tuple.GetObjectResultType();
                return new IL.Call(tupleType.GetMethods(options: GetMemberOptions.IgnoreInheritedMembers).Single(m => m.Name == "GetHashCode")) {
                    Arguments = { new IL.AddressOf(tuple) }
                };
            }
        }

        /// Implements IEquatable, Object.Equals, ==, != using the specified Self.Equals(Self) method. Does not implement GetHashCode.
        static IMethod ImplementEqualityCore(this VirtualType type, Func<IMethod, IL.ILFunction> equalsImplementation)
        {
            type.ImplementedInterfaces.Add(new ParameterizedType(type.Compilation.FindType(typeof(IEquatable<>)), new IType[] { type }));

            var eqMethod = new VirtualMethod(type, Accessibility.Public, "Equals", new [] { new DefaultParameter(type, "b") }, type.Compilation.FindType(typeof(bool)), isVirtual: !type.IsSealed);
            eqMethod.BodyFactory = () => equalsImplementation(eqMethod);

            type.Methods.Add(eqMethod);

            var eqOperator = new VirtualMethod(type, Accessibility.Public, "op_Equality", new [] { new DefaultParameter(type, "a"), new DefaultParameter(type, "b") }, type.Compilation.FindType(typeof(bool)), isStatic: true);
            eqOperator.BodyFactory = () => {
                var aParam = new IL.ILVariable(IL.VariableKind.Parameter, type, 0);
                var bParam = new IL.ILVariable(IL.VariableKind.Parameter, type, 1);
                IL.ILInstruction eqCall = new IL.Call(eqMethod) { Arguments = {
                        (bool)type.IsReferenceType ? new IL.LdLoc(aParam) : (IL.ILInstruction)new IL.LdLoca(aParam),
                        new IL.LdLoc(bParam)
                    }};
                var body =
                    (bool)type.IsReferenceType ?
                        new IL.IfInstruction(
                            new IL.Comp(IL.ComparisonKind.Equality, Sign.None, new IL.LdLoc(aParam), new IL.LdLoc(bParam)),
                            new IL.LdcI4(1),
                            AndAlso(
                                new IL.Comp(IL.ComparisonKind.Inequality, Sign.None, new IL.LdLoc(aParam), new IL.LdNull()),
                                eqCall
                            )
                        ) :
                        eqCall;

                return CreateExpressionFunction(eqOperator, body);
            };
            type.Methods.Add(eqOperator);

            var neqOperator = new VirtualMethod(type, Accessibility.Public, "op_Inequality", new [] { new DefaultParameter(type, "a"), new DefaultParameter(type, "b") }, type.Compilation.FindType(typeof(bool)), isStatic: true);
            neqOperator.BodyFactory = () => {
                var aParam = new IL.ILVariable(IL.VariableKind.Parameter, type, 0);
                var bParam = new IL.ILVariable(IL.VariableKind.Parameter, type, 1);
                var body =
                    new IL.Comp(IL.ComparisonKind.Equality, Sign.None,
                        new IL.Call(eqOperator) { Arguments = { new IL.LdLoc(aParam), new IL.LdLoc(bParam) } },
                        new IL.LdcI4(0));

                return CreateExpressionFunction(neqOperator, body);
            };
            type.Methods.Add(neqOperator);

            var objEquals = new VirtualMethod(type, Accessibility.Public, "Equals", new [] { new DefaultParameter(type.Compilation.FindType(typeof(object)), "b") }, type.Compilation.FindType(typeof(bool)), isOverride: true);

            objEquals.BodyFactory = () => {
                var tmpVar = new IL.ILVariable(IL.VariableKind.StackSlot, type, stackType: IL.StackType.O, 0);
                var otherParam = new IL.ILVariable(IL.VariableKind.Parameter, type.Compilation.FindType(typeof(object)), 0);
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                return CreateExpressionFunction(objEquals,
                    new IL.IfInstruction(
                        new IL.Comp(IL.ComparisonKind.Inequality, Sign.None, new IL.StLoc(tmpVar, new IL.IsInst(new IL.LdLoc(otherParam), type)), new IL.LdNull()),
                        new IL.Call(eqMethod) { Arguments = { new IL.LdLoc(thisParam), new IL.LdLoc(tmpVar) } },
                        new IL.LdcI4(0)
                    )
                );
            };
            type.Methods.Add(objEquals);
            return eqMethod;
        }

        public static IL.ILInstruction AccessMember(this IL.ILInstruction target, IMember p) =>
                 p is IProperty property ? new IL.Call(property.Getter) { Arguments = { target } } :
                 p is IField field ? (IL.ILInstruction)new IL.LdObj(new IL.LdFlda(target, field), field.ReturnType) :
                 throw new NotSupportedException($"{p.GetType()}");

        public static IMethod ImplementEquality(this VirtualType type, params IMember[] properties) =>
            ImplementEquality(type, properties.Select(p =>
                (p.ReturnType, (Func<IL.ILInstruction, IL.ILInstruction>)(target => target.AccessMember(p)))
            ).ToArray());

        public static IMethod ImplementEquality(this VirtualType type, params (IType type, Func<IL.ILInstruction, IL.ILInstruction> getter)[] properties)
        {
            type.ImplementGetHashCode(properties);
            return type.ImplementEqualityCore((eqMethod) =>
            {
                var @thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                var otherParam = new IL.ILVariable(IL.VariableKind.Parameter, type, 0);
                var body =
                    AndAlso(properties.Select(p => EqualsExpression(p.type, p.getter(new IL.LdLoc(@thisParam)), p.getter(new IL.LdLoc(otherParam)))));

                if (type.IsReferenceType == true)
                    body = new IL.IfInstruction(
                        new IL.Comp(IL.ComparisonKind.Equality, Sign.None, new IL.LdLoc(thisParam), new IL.LdLoc(otherParam)),
                        new IL.LdcI4(1),
                        body
                    );
                return CreateExpressionFunction(eqMethod, body);
            });
        }

        static IMethod ImplementGetHashCode(this VirtualType type, (IType type, Func<IL.ILInstruction, IL.ILInstruction> getter)[] properties)
        {
            var getHashCode = new VirtualMethod(type, Accessibility.Public, "GetHashCode", new IParameter[0], type.Compilation.FindType(typeof(int)), isOverride: true);
            getHashCode.BodyFactory = () =>
            {
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                var body = CombineHashCodes(type.Compilation, null, properties.Select(p => GetHashCodeExpression(p.type, p.getter(new IL.LdLoc(thisParam)))).ToArray()); // TODO: seed
                return CreateExpressionFunction(getHashCode, body);
            };
            type.Methods.Add(getHashCode);
            return getHashCode;
        }

        /// Implements IEquatable.Equals, Object.Equals, !=, == using a abstract EqualsCore method
        public static (IMethod equalsCore, IMethod equals) ImplementEqualityForBase(this VirtualType type)
        {
            Debug.Assert(type.IsAbstract);
            var methodName = SymbolNamer.NameMethod(type, "EqualsCore", 0, new[] { type });
            var eqCoreMethod = new VirtualMethod(type, Accessibility.ProtectedAndInternal, methodName, new [] { new DefaultParameter(type, "b") }, type.Compilation.FindType(typeof(bool)), isAbstract: true);
            type.Methods.Add(eqCoreMethod);
            return (eqCoreMethod, type.ImplementEqualityCore(eqMethod =>
            {
                var @thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);
                var otherParam = new IL.ILVariable(IL.VariableKind.Parameter, type, 0);
                var body = new IL.IfInstruction(
                    new IL.Comp(IL.ComparisonKind.Equality, Sign.None, new IL.LdLoc(thisParam), new IL.LdLoc(otherParam)),
                    new IL.LdcI4(1),
                    new IL.CallVirt(eqCoreMethod) { Arguments = { new IL.LdLoc(thisParam), new IL.LdLoc(otherParam) } }
                );
                return CreateExpressionFunction(eqMethod, body);
            }));
        }

        /// Implements EqualsCore (method required by ImplementEqualityForBase) and GetHashCode methods using the specified properties
        public static IMethod ImplementEqualityForCase(this VirtualType type, IMethod overridesMethod, params IMember[] properties) =>
            type.ImplementEqualityForCase(overridesMethod, properties.Select(p =>
                (p.ReturnType, (Func<IL.ILInstruction, IL.ILInstruction>)(target => target.AccessMember(p)))
            ).ToArray());
        /// Implements EqualsCore (method required by ImplementEqualityForBase) and GetHashCode methods using the specified properties
        public static IMethod ImplementEqualityForCase(this VirtualType type, IMethod overridesMethod, params (IType type, Func<IL.ILInstruction, IL.ILInstruction> getter)[] properties)
        {
            Debug.Assert(!type.IsAbstract);
            Debug.Assert(type.DirectBaseType != null);
            Debug.Assert(type.GetAllBaseTypes().Contains(overridesMethod.DeclaringType));

            type.ImplementGetHashCode(properties);
            var eqCoreMethod = new VirtualMethod(type, overridesMethod.Accessibility, overridesMethod.Name, overridesMethod.Parameters, type.Compilation.FindType(typeof(bool)), isOverride: true);
            eqCoreMethod.BodyFactory = () => {
                var tmpVar = new IL.ILVariable(IL.VariableKind.StackSlot, type, stackType: IL.StackType.O, 0);
                var otherParam = new IL.ILVariable(IL.VariableKind.Parameter, type.Compilation.FindType(typeof(object)), 0);
                var thisParam = new IL.ILVariable(IL.VariableKind.Parameter, type, -1);

                var eqCore =
                    AndAlso(properties.Select(p => EqualsExpression(p.type, p.getter(new IL.LdLoc(@thisParam)), p.getter(new IL.LdLoc(otherParam)))));

                var body =
                    // do typecheck of the parameter while assigning the tmpVar
                    new IL.IfInstruction(
                        new IL.Comp(IL.ComparisonKind.Inequality, Sign.None, new IL.StLoc(tmpVar, new IL.IsInst(new IL.LdLoc(otherParam), type)), new IL.LdNull()),
                        eqCore,
                        new IL.LdcI4(0)
                    );
                return CreateExpressionFunction(eqCoreMethod, body);
            };
            type.Methods.Add(eqCoreMethod);
            return eqCoreMethod;
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
    }
}
