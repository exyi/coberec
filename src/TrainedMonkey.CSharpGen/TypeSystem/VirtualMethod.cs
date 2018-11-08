using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using ICSharpCode.Decompiler.Util;

namespace TrainedMonkey.CSharpGen.TypeSystem
{

    public class VirtualMethod : IMethod, IMethodWithDefinition, IHideableMember
    {
        public VirtualMethod(ITypeDefinition declaringType, Accessibility accessibility, string name, IReadOnlyList<IParameter> parameters, IType returnType, bool isHidden = false)
        {
            this.DeclaringTypeDefinition = declaringType;
            this.ReturnType = returnType;
            this.Parameters = parameters;
            this.Name = name;
            this.Accessibility = accessibility;
            this.IsHidden = isHidden;
        }

        public IReadOnlyList<ITypeParameter> TypeParameters => EmptyList<ITypeParameter>.Instance;

        public IReadOnlyList<IType> TypeArguments => EmptyList<IType>.Instance;

        public bool IsExtensionMethod => false;

        public bool IsConstructor => false;

        public bool IsDestructor => false;

        public bool IsOperator => false;

        public bool HasBody => this.BodyFactory != null;

        public bool IsAccessor => false;

        public IMember AccessorOwner => null;

        public IMethod ReducedFrom => null;

        public IReadOnlyList<IParameter> Parameters { get; }

        public IMember MemberDefinition => this;

        public IType ReturnType { get; }

        public IEnumerable<IMember> ExplicitlyImplementedInterfaceMembers => EmptyList<IMember>.Instance;

        public bool IsExplicitInterfaceImplementation => false;

        public bool IsVirtual => false;

        public bool IsOverride => false;

        public bool IsOverridable => false;

        public TypeParameterSubstitution Substitution => TypeParameterSubstitution.Identity;

        public EntityHandle MetadataToken => default;

        public string Name { get; }

        public ITypeDefinition DeclaringTypeDefinition { get; }

        public IType DeclaringType => this.DeclaringTypeDefinition;

        public IModule ParentModule => this.DeclaringTypeDefinition.ParentModule;

        public Accessibility Accessibility { get; }

        public bool IsStatic => false;

        public bool IsAbstract => false;

        public bool IsSealed => false;

        public SymbolKind SymbolKind => Name == ".ctor" || Name == ".cctor" ? SymbolKind.Constructor : SymbolKind.Method;

        public ICompilation Compilation => this.ParentModule.Compilation;

        public string FullName => $"{this.DeclaringType.FullName}.{this.Name}";

        public string ReflectionName => $"{this.DeclaringType.ReflectionName}.{this.Name}";

        public string Namespace => this.DeclaringType.Namespace;

        public bool Equals(IMember obj, TypeVisitor typeNormalization)
        {
            return obj is IMethod m &&
                   m.Name == this.Name &&
                   m.Parameters.Count == this.Parameters.Count &&
                   this.DeclaringType.AcceptVisitor(typeNormalization).Equals(m.DeclaringType.AcceptVisitor(typeNormalization));
        }

        public readonly List<IAttribute> Attributes = new List<IAttribute>();
        public IEnumerable<IAttribute> GetAttributes() => Attributes;

        public Func<ILFunction> BodyFactory { get; set; }

        public bool IsHidden { get; }

        public ILFunction GetBody()
        {
            return BodyFactory?.Invoke();
        }

        public readonly List<IAttribute> ReturnTypeAttributes = new List<IAttribute>();
        public IEnumerable<IAttribute> GetReturnTypeAttributes() => ReturnTypeAttributes;

        public bool HasFlag(MethodAttributes attributes)
        {
            var result = true;
            for (int i = 0; i < 16; i++)
            switch (attributes & (MethodAttributes)(1 << i))
            {
                case MethodAttributes.Abstract:
                    result &= this.IsAbstract;
                    break;
                case MethodAttributes.Assembly:
                    result &= this.Accessibility != Accessibility.Private && this.Accessibility != Accessibility.Protected && this.Accessibility != Accessibility.ProtectedAndInternal;
                    break;
                case MethodAttributes.CheckAccessOnOverride:
                    result &= true;
                    break;
                case MethodAttributes.FamANDAssem:
                    result &= this.Accessibility == Accessibility.ProtectedAndInternal;
                    break;
                case MethodAttributes.Family:
                    result &= this.Accessibility == Accessibility.Protected;
                    break;
                case MethodAttributes.FamORAssem:
                    result &= this.Accessibility == Accessibility.ProtectedOrInternal;
                    break;
                case MethodAttributes.Final:
                    result &= this.IsSealed;
                    break;
                case MethodAttributes.HasSecurity:
                    result &= false;
                    break;
                case MethodAttributes.HideBySig:
                    result &= false;
                    break;
                case MethodAttributes.MemberAccessMask:
                    break;
                case MethodAttributes.NewSlot:
                    result &= this.IsOverride;
                    break;
                case MethodAttributes.PinvokeImpl:
                    result &= false;
                    break;
                case MethodAttributes.Private:
                    result &= this.Accessibility == Accessibility.Private;
                    break;
                case MethodAttributes.Public:
                    result &= this.Accessibility == Accessibility.Public;
                    break;
                case MethodAttributes.RequireSecObject:
                    result &= false;
                    break;
                case MethodAttributes.ReservedMask:
                    break;
                case MethodAttributes.RTSpecialName:
                    break;
                case MethodAttributes.SpecialName:
                    break;
                case MethodAttributes.Static:
                    result &= this.IsStatic;
                    break;
                case MethodAttributes.UnmanagedExport:
                    break;
                case MethodAttributes.Virtual:
                    result &= this.IsVirtual;
                    break;
                default:
                    break;
            }
            return result;
        }

        public IMethod Specialize(TypeParameterSubstitution substitution)
        {
            throw new NotSupportedException();
        }

        IMember IMember.Specialize(TypeParameterSubstitution substitution) => this.Specialize(substitution);
    }

    interface IHideableMember
    {
        bool IsHidden { get; }
    }
}
