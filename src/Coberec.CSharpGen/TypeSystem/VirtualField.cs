using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace Coberec.CSharpGen.TypeSystem
{
    public sealed class VirtualField : IField, IHideableMember
    {
        public VirtualField(ITypeDefinition declaringTypeDefinition, Accessibility accessibility, string name, IType returnType, bool isReadOnly = true, bool isVolatile = false, bool isStatic = false, bool isHidden = false)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.IsReadOnly = isReadOnly;
            this.IsVolatile = isVolatile;
            this.ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
            this.DeclaringTypeDefinition = declaringTypeDefinition ?? throw new ArgumentNullException(nameof(declaringTypeDefinition));
            this.Accessibility = accessibility;
            this.IsStatic = isStatic;
            this.IsConst = false;
            this.ConstantValue = null;
            this.IsHidden = isHidden;
        }

        public VirtualField(ITypeDefinition declaringTypeDefinition, Accessibility accessibility, string name, IType returnType, bool isConst, object defaltValue)
        {
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.IsReadOnly = true;
            this.IsVolatile = false;
            this.ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
            this.DeclaringTypeDefinition = declaringTypeDefinition ?? throw new ArgumentNullException(nameof(declaringTypeDefinition));
            this.Accessibility = accessibility;
            this.IsStatic = true;
            this.IsConst = isConst;
            this.ConstantValue = defaltValue;
        }

        public string Name { get; }

        public bool IsReadOnly { get; }

        public bool IsVolatile { get; }

        public IMember MemberDefinition => this;

        public IType ReturnType { get; }

        public IEnumerable<IMember> ExplicitlyImplementedInterfaceMembers => Enumerable.Empty<IMember>();

        public bool IsExplicitInterfaceImplementation => false;

        public bool IsVirtual => false;

        public bool IsOverride => false;

        public bool IsOverridable => false;

        public TypeParameterSubstitution Substitution => TypeParameterSubstitution.Identity;

        public EntityHandle MetadataToken => default;

        public ITypeDefinition DeclaringTypeDefinition { get; }

        public IType DeclaringType => DeclaringTypeDefinition;

        public IModule ParentModule => DeclaringTypeDefinition.ParentModule;

        public Accessibility Accessibility { get; }

        public bool IsStatic { get; }

        public bool IsAbstract => false;

        public bool IsSealed => false;

        public ICompilation Compilation => ParentModule.Compilation;

        public string FullName => $"{DeclaringType.FullName}.{Name}";

        public string ReflectionName => $"{DeclaringType.ReflectionName}.{Name}";

        public string Namespace => DeclaringType.Namespace;

        public IType Type => this.ReturnType;

        public bool IsConst { get; }

        public object ConstantValue { get; }

        public SymbolKind SymbolKind => SymbolKind.Field;

        public bool IsHidden { get; }

        public bool Equals(IMember obj, TypeVisitor typeNormalization) =>
            this.DeclaringTypeDefinition.AcceptVisitor(typeNormalization).Equals(
                obj.DeclaringTypeDefinition.AcceptVisitor(typeNormalization)) &&
            Name == Name;

        public readonly List<IAttribute> Attributes = new List<IAttribute>();

        public IEnumerable<IAttribute> GetAttributes() => Attributes;

        public IMember Specialize(TypeParameterSubstitution substitution)
        {
            throw new NotImplementedException();
        }
    }
}
