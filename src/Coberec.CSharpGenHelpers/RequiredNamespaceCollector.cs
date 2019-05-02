using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.Decompiler.Disassembler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using ICSharpCode.Decompiler.Util;

using static ICSharpCode.Decompiler.Metadata.ILOpCodeExtensions;

namespace Coberec.CSharpGen.Emit
{
	public class RequiredNamespaceCollector_Hacked
	{
		public static void CollectNamespaces(IModule module, HashSet<string> namespaces)
		{
			foreach (var type in module.TypeDefinitions) {
				CollectNamespaces(type, module, namespaces);
			}
			CollectAttributeNamespaces(module, namespaces);
		}

		public static void CollectAttributeNamespaces(IModule module, HashSet<string> namespaces)
		{
			HandleAttributes(module.GetAssemblyAttributes(), namespaces);
			HandleAttributes(module.GetModuleAttributes(), namespaces);
		}

		static readonly ICSharpCode.Decompiler.TypeSystem.GenericContext genericContext = default;

		public static void CollectNamespaces(IEntity entity, IModule module,
			HashSet<string> namespaces)
		{
			if (entity == null)
				return;
			switch (entity) {
				case ITypeDefinition td:
					namespaces.Add(td.Namespace);
					HandleAttributes(td.GetAttributes(), namespaces);
					HandleTypeParameters(td.TypeParameters, namespaces);

					foreach (var baseType in td.DirectBaseTypes) {
						CollectNamespacesForTypeReference(baseType, namespaces);
					}

					foreach (var nestedType in td.NestedTypes) {
						CollectNamespaces(nestedType, module, namespaces);
					}

					foreach (var field in td.Fields) {
						CollectNamespaces(field, module, namespaces);
					}

					foreach (var property in td.Properties) {
						CollectNamespaces(property, module, namespaces);
					}

					foreach (var @event in td.Events) {
						CollectNamespaces(@event, module, namespaces);
					}

					foreach (var method in td.Methods) {
						CollectNamespaces(method, module, namespaces);
					}
					break;
				case IField field:
					HandleAttributes(field.GetAttributes(), namespaces);
					CollectNamespacesForTypeReference(field.ReturnType, namespaces);
					break;
				case IMethod method:
					HandleAttributes(method.GetAttributes(), namespaces);
					HandleAttributes(method.GetReturnTypeAttributes(), namespaces);
					CollectNamespacesForTypeReference(method.ReturnType, namespaces);
					foreach (var param in method.Parameters) {
						HandleAttributes(param.GetAttributes(), namespaces);
						CollectNamespacesForTypeReference(param.Type, namespaces);
					}
					HandleTypeParameters(method.TypeParameters, namespaces);
					if (method.HasBody && method is IMethodWithDefinition mDef) {
						CollectNamespacesFromMethodBody(method, mDef.GetBody(), namespaces);
					}
					break;
				case IProperty property:
					HandleAttributes(property.GetAttributes(), namespaces);
					CollectNamespaces(property.Getter, module, namespaces);
					CollectNamespaces(property.Setter, module, namespaces);
					break;
				case IEvent @event:
					HandleAttributes(@event.GetAttributes(), namespaces);
					CollectNamespaces(@event.AddAccessor, module, namespaces);
					CollectNamespaces(@event.RemoveAccessor, module, namespaces);
					break;
			}
		}

		static void CollectNamespacesForTypeReference(IType type, HashSet<string> namespaces)
		{
			switch (type) {
				case ParameterizedType parameterizedType:
					namespaces.Add(parameterizedType.Namespace);
					CollectNamespacesForTypeReference(parameterizedType.GenericType, namespaces);
					foreach (var arg in parameterizedType.TypeArguments)
						CollectNamespacesForTypeReference(arg, namespaces);
					break;
				case TypeWithElementType typeWithElementType:
					CollectNamespacesForTypeReference(typeWithElementType.ElementType, namespaces);
					break;
				case TupleType tupleType:
					foreach (var elementType in tupleType.ElementTypes) {
						CollectNamespacesForTypeReference(elementType, namespaces);
					}
					break;
				default:
					namespaces.Add(type.Namespace);
					break;
			}
		}

		public static void HandleAttributes(IEnumerable<IAttribute> attributes, HashSet<string> namespaces)
		{
			foreach (var attr in attributes) {
				namespaces.Add(attr.AttributeType.Namespace);
				foreach (var arg in attr.FixedArguments) {
					HandleAttributeValue(arg.Type, arg.Value, namespaces);
				}
				foreach (var arg in attr.NamedArguments) {
					HandleAttributeValue(arg.Type, arg.Value, namespaces);
				}
			}
		}

		static void HandleAttributeValue(IType type, object value, HashSet<string> namespaces)
		{
			CollectNamespacesForTypeReference(type, namespaces);
			if (value is IType typeofType)
				CollectNamespacesForTypeReference(typeofType, namespaces);
			if (value is ImmutableArray<CustomAttributeTypedArgument<IType>> arr) {
				foreach (var element in arr) {
					HandleAttributeValue(element.Type, element.Value, namespaces);
				}
			}
		}

		static void HandleTypeParameters(IEnumerable<ITypeParameter> typeParameters, HashSet<string> namespaces)
		{
			foreach (var typeParam in typeParameters) {
				HandleAttributes(typeParam.GetAttributes(), namespaces);

				foreach (var constraint in typeParam.DirectBaseTypes) {
					CollectNamespacesForTypeReference(constraint, namespaces);
				}
			}
		}

		static void CollectNamespacesFromMethodBody(IMethod method, ILFunction body, HashSet<string> namespaces)
		{
			body.AcceptVisitor(new ILCollectionVisitor(namespaces));
			foreach(var v in body.Variables) {
				CollectNamespacesForTypeReference(v.Type, namespaces);
			}
			foreach(var node in body.Descendants) {
				if (node is LdsFlda staticField) CollectNamespacesForMemberReference(staticField.Field, namespaces);
				else if (node is CallInstruction call && call.Method.IsStatic) CollectNamespacesForMemberReference(call.Method, namespaces);
				else if (node is NewObj newObj) CollectNamespacesForTypeReference(newObj.Method.DeclaringType, namespaces);
			}
		}

        class ILCollectionVisitor : ILVisitor
        {
            private readonly HashSet<string> namespaces;

            public ILCollectionVisitor(HashSet<string> namespaces)
            {
                this.namespaces = namespaces;
            }

			protected override void VisitCastClass(CastClass inst) => CollectNamespacesForTypeReference(inst.Type, this.namespaces);
			protected override void VisitIsInst(IsInst inst) => CollectNamespacesForTypeReference(inst.Type, this.namespaces);
			protected override void VisitCall(Call inst) => CollectNamespacesForMemberReference(inst.Method, this.namespaces);
			// BIG TODO

            protected override void Default(ILInstruction inst)
            {
				foreach (var c in inst.Children)
					c.AcceptVisitor(this);
            }
        }

        static void CollectNamespacesForMemberReference(IMember member, HashSet<string> namespaces)
		{
			switch (member) {
				case IField field:
					CollectNamespacesForTypeReference(field.DeclaringType, namespaces);
					CollectNamespacesForTypeReference(field.ReturnType, namespaces);
					break;
				case IMethod method:
					CollectNamespacesForTypeReference(method.DeclaringType, namespaces);
					CollectNamespacesForTypeReference(method.ReturnType, namespaces);
					foreach (var param in method.Parameters)
						CollectNamespacesForTypeReference(param.Type, namespaces);
					foreach (var arg in method.TypeArguments)
						CollectNamespacesForTypeReference(arg, namespaces);
					break;
			}
		}
	}
}
