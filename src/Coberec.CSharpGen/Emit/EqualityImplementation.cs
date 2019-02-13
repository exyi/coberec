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
using static Coberec.CSharpGen.Emit.EmitExtensions;

namespace Coberec.CSharpGen.Emit
{
    public static class EqualityImplementation
    {
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
            var compilation = propertyType.GetDefinition().Compilation;
            if (propertyType.GetStackType() != IL.StackType.O)
            {
                return new IL.Comp(IL.ComparisonKind.Equality, needsBoxing ? IL.ComparisonLiftingKind.CSharp : IL.ComparisonLiftingKind.None, propertyType.GetStackType(), Sign.None, @this, other);
            }
            // enumerable types tend to be reference-equality even though they have IStructuralEquality overridden
            else if (seqInterface != null && (enumerableInterface != null || eqInterface == null))
            {
                return InvokeInterfaceMethod(compilation.FindType(typeof(System.Collections.IEqualityComparer)).GetMethods(options: GetMemberOptions.IgnoreInheritedMembers).Single(m => m.Name == "Equals"), seqInterface,
                    new IL.CallVirt(compilation.FindType(typeof(System.Collections.StructuralComparisons)).GetProperties().Single(p => p.Name == "StructuralEqualityComparer").Getter),
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
            else if (propertyType.Kind == TypeKind.Interface)
            {
                Debug.Assert(!needsBoxing); // interface should not be in Nullable<_>
                // if the type is a interface, let's compare it using the object.Equals method and hope that it will be implemented correctly in the implementation
                var objectEqualsMethod = compilation.FindType(KnownTypeCode.Object).GetMethods(m => m.Parameters.Count == 2 && m.Name == "Equals" && m.IsStatic).Single();
                return new IL.Call(objectEqualsMethod) { Arguments = { @this, other } };
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

                /// Implements IEquatable, Object.Equals, ==, != using the specified Self.Equals(Self) method. Does not implement GetHashCode.
        static IMethod ImplementEqualityCore(this VirtualType type, Func<IMethod, IL.ILFunction> equalsImplementation)
        {
            type.ImplementedInterfaces.Add(new ParameterizedType(type.Compilation.FindType(typeof(IEquatable<>)), new IType[] { type }));

            var eqMethod = new VirtualMethod(type, Accessibility.Public, "Equals", new [] { new VirtualParameter(type, "b") }, type.Compilation.FindType(typeof(bool)), isVirtual: !type.IsSealed);
            eqMethod.BodyFactory = () => equalsImplementation(eqMethod);

            type.Methods.Add(eqMethod);

            var eqOperator = new VirtualMethod(type, Accessibility.Public, "op_Equality", new [] { new VirtualParameter(type, "a"), new VirtualParameter(type, "b") }, type.Compilation.FindType(typeof(bool)), isStatic: true);
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

            var neqOperator = new VirtualMethod(type, Accessibility.Public, "op_Inequality", new [] { new VirtualParameter(type, "a"), new VirtualParameter(type, "b") }, type.Compilation.FindType(typeof(bool)), isStatic: true);
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

            var objEquals = new VirtualMethod(type, Accessibility.Public, "Equals", new [] { new VirtualParameter(type.Compilation.FindType(typeof(object)), "b") }, type.Compilation.FindType(typeof(bool)), isOverride: true);

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

        /// Implements IEquatable.Equals, Object.Equals, !=, == using a abstract EqualsCore method
        public static (IMethod equalsCore, IMethod equals) ImplementEqualityForBase(this VirtualType type)
        {
            Debug.Assert(type.IsAbstract);
            var methodName = SymbolNamer.NameMethod(type, "EqualsCore", 0, new[] { type });
            var eqCoreMethod = new VirtualMethod(type, Accessibility.ProtectedAndInternal, methodName, new [] { new VirtualParameter(type, "b") }, type.Compilation.FindType(typeof(bool)), isAbstract: true);
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
    }
}
