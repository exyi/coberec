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

namespace Coberec.CSharpGen.TypeSystem
{

    public sealed class VirtualMethod : IMethod, IMethodWithDefinition, IHideableMember, IWithDoccomment
    {
        public VirtualMethod(ITypeDefinition declaringType, Accessibility accessibility, string name, IReadOnlyList<IParameter> parameters, IType returnType, bool isOverride = false, bool isVirtual = false, bool isSealed = false, bool isAbstract = false, bool isStatic = false, bool isHidden = false, IEnumerable<IMember> explicitImplementations = null, string doccomment = null)
        {
            this.DeclaringTypeDefinition = declaringType ?? throw new ArgumentNullException(nameof(declaringType)); ;
            this.ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
            this.Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Accessibility = accessibility;
            this.IsHidden = isHidden;
            this.IsOverride = isOverride;
            this.IsVirtual = isVirtual;
            this.IsSealed = isSealed;
            this.IsAbstract = isAbstract;
            this.IsStatic = isStatic;
            this.TypeParameters = Array.Empty<ITypeParameter>();
            this.ExplicitlyImplementedInterfaceMembers = explicitImplementations?.ToArray() ?? Array.Empty<IMember>();
            this.ThisIsRefReadOnly = declaringType.IsReadOnly;
            this.Doccomment = doccomment;
        }

        /// generic method require dependency on the type parameters, which have dependency on its owner :/ So everything has to be lazy loaded after generic parameters are instantiated :(
        public VirtualMethod(ITypeDefinition declaringType, Accessibility accessibility, string name, Func<IMethod, IReadOnlyList<IParameter>> parameters, Func<IMethod, IType> returnType, bool isOverride = false, bool isVirtual = false, bool isSealed = false, bool isAbstract = false, bool isStatic = false, bool isHidden = false, Func<IEntity, int, ITypeParameter>[] typeParameters = null, IEnumerable<IMember> explicitImplementations = null, string doccomment = null)
        {
            this.DeclaringTypeDefinition = declaringType ?? throw new ArgumentNullException(nameof(declaringType)); ;
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.Accessibility = accessibility;
            this.IsHidden = isHidden;
            this.IsOverride = isOverride;
            this.IsVirtual = isVirtual;
            this.IsSealed = isSealed;
            this.IsAbstract = isAbstract;
            this.IsStatic = isStatic;
            this.ExplicitlyImplementedInterfaceMembers = explicitImplementations?.ToArray() ?? Array.Empty<IMember>();

            this.TypeParameters = typeParameters?.Select((t, i) => t(this, i)).ToArray() ?? Array.Empty<ITypeParameter>();
            this.ReturnType = returnType(this) ?? throw new ArgumentNullException(nameof(returnType));
            this.Parameters = parameters(this) ?? throw new ArgumentNullException(nameof(parameters));
            this.ThisIsRefReadOnly = declaringType.IsReadOnly;
            this.Doccomment = doccomment;
        }

        public IReadOnlyList<ITypeParameter> TypeParameters { get; }

        public IReadOnlyList<IType> TypeArguments => EmptyList<IType>.Instance;

        public bool IsExtensionMethod => false;

        public bool IsConstructor => this.SymbolKind == SymbolKind.Constructor;

        public bool IsDestructor => this.Name == "Finalize";

        public bool IsOperator => this.Name.StartsWith("op_");

        public bool HasBody => this.BodyFactory != null;

        public bool IsAccessor => AccessorOwner != null;

        public IMember AccessorOwner { get; private set; }
        public MethodSemanticsAttributes AccessorKind { get; private set; }

        public void SetAccessorOwner(IMember owner, MethodSemanticsAttributes accKind)
        {
            this.AccessorOwner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.AccessorKind = accKind;
        }

        public IMethod ReducedFrom => null;

        public IReadOnlyList<IParameter> Parameters { get; }

        public IMember MemberDefinition => this;

        public IType ReturnType { get; }

        public IEnumerable<IMember> ExplicitlyImplementedInterfaceMembers { get; }

        public bool IsExplicitInterfaceImplementation => ExplicitlyImplementedInterfaceMembers.Count() > 0;

        public bool IsVirtual { get; }

        public bool IsOverride { get; }

        public bool IsOverridable => !this.IsSealed && (this.IsVirtual || this.IsOverride || this.IsAbstract);

        public TypeParameterSubstitution Substitution => TypeParameterSubstitution.Identity;

        public EntityHandle MetadataToken => default;

        public string Name { get; }

        public ITypeDefinition DeclaringTypeDefinition { get; }

        public IType DeclaringType => this.DeclaringTypeDefinition;

        public IModule ParentModule => this.DeclaringTypeDefinition.ParentModule;

        public Accessibility Accessibility { get; }

        public bool IsStatic { get; }

        public bool IsAbstract { get; }

        public bool IsSealed { get; }

        public SymbolKind SymbolKind => Name == ".ctor" || Name == ".cctor" ? SymbolKind.Constructor :
                                        this.IsOperator ? SymbolKind.Operator : // TODO
                                        SymbolKind.Method;

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

        public bool IsHidden { get; set; }

        public bool IsPartial { get; set; }

        public bool ReturnTypeIsRefReadOnly => GetReturnTypeAttributes().Any(a => a.AttributeType.IsKnownType(KnownAttribute.IsReadOnly));

        public bool IsLocalFunction { get; }

        public bool ThisIsRefReadOnly { get; set; }

        public string Doccomment { get; }

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

        public IMethod Specialize(TypeParameterSubstitution substitution) => SpecializedMethod.Create(this, substitution);

        IMember IMember.Specialize(TypeParameterSubstitution substitution) => this.Specialize(substitution);

		public override string ToString()
		{
			return $"{DeclaringType?.ReflectionName}.{Name}";
		}
    }

    public interface IHideableMember
    {
        bool IsHidden { get; }
    }

    public interface IWithDoccomment
    {
        string Doccomment { get; }
    }
}
