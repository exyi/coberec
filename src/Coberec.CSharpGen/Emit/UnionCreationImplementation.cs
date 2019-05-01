using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Coberec.CSharpGen.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem;
using IL = ICSharpCode.Decompiler.IL;

namespace Coberec.CSharpGen.Emit
{
    public static class UnionCreationImplementation
    {
        public static IMethod ImplementBasicCaseFactory(this VirtualType type, string caseName, IMethod caseCtor)
        {
            var valueType = caseCtor.Parameters.Single().Type;
            var caseFactory = new VirtualMethod(type, Accessibility.Public,
                SymbolNamer.NameMethod(type, caseName, 0, new [] { valueType }),
                new[] { new VirtualParameter(valueType, "item") },
                returnType: type,
                isStatic: true
            );
            caseFactory.BodyFactory = () =>
                EmitExtensions.CreateExpressionFunction(caseFactory,
                    new IL.NewObj(caseCtor) { Arguments = { new IL.LdLoc(new IL.ILVariable(IL.VariableKind.Parameter, valueType, 0)) } }
                );
            type.Methods.Add(caseFactory);
            return caseFactory;
        }

        public static IMethod[] ImplementAllIntoCaseConversions(this VirtualType type, IMethod[] caseCtors, bool isImplicit = true)
        {
            var dupTypes = new HashSet<IType>(
                caseCtors
                .GroupBy(c => c.Parameters.Single().Type)
                .Where(t => t.Count() > 1)
                .Select(t => t.Key));

            var result =
                from ctor in caseCtors
                let valueType = ctor.Parameters.Single().Type
                where valueType.Kind != TypeKind.Interface
                where valueType != type
                where !dupTypes.Contains(valueType)
                select ImplementIntoCaseConversion(type, valueType, ctor, isImplicit);
            return result.ToArray();
        }

        static IMethod ImplementIntoCaseConversion(this VirtualType type, IType valueType, IMethod caseCtor, bool isImplicit = true)
        {
            Debug.Assert(valueType == caseCtor.Parameters.Single().Type);
            Debug.Assert(valueType.Kind != TypeKind.Interface); // can't have conversion from interface
            Debug.Assert(valueType != type); // can't have conversion from itself

            var caseFactory = new VirtualMethod(type, Accessibility.Public,
                isImplicit ? "op_Implicit" : "op_Explicit",
                new[] { new VirtualParameter(valueType, "item") },
                returnType: type,
                isStatic: true
            );
            caseFactory.BodyFactory = () =>
                EmitExtensions.CreateExpressionFunction(caseFactory,
                    new IL.NewObj(caseCtor) { Arguments = { new IL.LdLoc(new IL.ILVariable(IL.VariableKind.Parameter, valueType, 0)) } }
                );
            type.Methods.Add(caseFactory);
            return caseFactory;
        }


        /// Implements "forwarding constructor" that creates creates an instance of the inner class from it's constructor parameters
        public static ImmutableArray<IMethod> TryImplementForwardingCaseFactory(this VirtualType type, string caseName, IMethod caseCtor)
        {
            var valueType = caseCtor.Parameters.Single().Type;

            // allowed only for "our" types
            if (!(valueType is VirtualType))
                return ImmutableArray<IMethod>.Empty;

            return
                valueType.GetConstructors(c => c.Accessibility == Accessibility.Public)
                .Select(ctor => type.ImplementForwardingCaseFactory(caseName, caseCtor, ctor))
                .ToImmutableArray();
        }

        public static IMethod ImplementForwardingCaseFactory(this VirtualType type, string caseName, IMethod caseCtor, IMethod valueTypeCtor)
        {
            var valueType = caseCtor.Parameters.Single().Type;
            Debug.Assert(valueType == valueTypeCtor.DeclaringType);

            var caseFactory = new VirtualMethod(type, Accessibility.Public,
                SymbolNamer.NameMethod(type, caseName, 0, valueTypeCtor.Parameters),
                valueTypeCtor.Parameters,
                returnType: type,
                isStatic: true
            );
            caseFactory.BodyFactory = () => {
                var call = new IL.NewObj(valueTypeCtor);
                call.Arguments.AddRange(valueTypeCtor.Parameters.Select((p, i) => new IL.LdLoc(new IL.ILVariable(IL.VariableKind.Parameter, p.Type, i))));
                return EmitExtensions.CreateExpressionFunction(caseFactory,
                    new IL.NewObj(caseCtor) { Arguments = { call } }
                );
            };
            type.Methods.Add(caseFactory);
            return caseFactory;
        }
    }
}
