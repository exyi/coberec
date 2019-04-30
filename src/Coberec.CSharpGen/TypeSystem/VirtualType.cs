using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using ICSharpCode.Decompiler.Util;

namespace Coberec.CSharpGen.TypeSystem
{

    public sealed class VirtualType : ITypeDefinition, IHideableMember
    {
        public VirtualType(TypeKind kind, Accessibility accessibility, FullTypeName name, bool isStatic, bool isSealed, bool isAbstract, ITypeDefinition declaringType = null, IModule parentModule = null, bool isHidden = false)
        {
            if (declaringType == null && parentModule == null) throw new ArgumentException("declaringType or parentModule parameter must be non-null");
            if (name.Name == null) throw new ArgumentException(nameof(name));
            this.Kind = kind;
            this.DeclaringTypeDefinition = declaringType;
            this.Accessibility = accessibility;
            this.FullTypeName = name;
            this.IsAbstract = isAbstract;
            this.IsStatic = isStatic;
            this.IsSealed = isSealed;
            this.ParentModule = parentModule ?? declaringType.ParentModule ?? throw new ArgumentNullException(nameof(parentModule));
            this.IsHidden = isHidden;
        }

        public TypeKind Kind { get; }

        public bool? IsReferenceType => this.Kind == TypeKind.Class || this.Kind == TypeKind.Interface;

        public bool IsByRefLike => false;

        public ITypeDefinition DeclaringTypeDefinition { get; }
        public IType DeclaringType => this.DeclaringTypeDefinition;

        public int TypeParameterCount => 0;

        public IReadOnlyList<ITypeParameter> TypeParameters => EmptyList<ITypeParameter>.Instance;

        public IReadOnlyList<IType> TypeArguments => EmptyList<IType>.Instance;

        public IType DirectBaseType { get; set; } = null;
        public List<IType> ImplementedInterfaces = new List<IType>();
        public IEnumerable<IType> DirectBaseTypes => ImplementedInterfaces.Concat(DirectBaseType == null ? Enumerable.Empty<IType>() : new [] { DirectBaseType });

        public string FullName => this.FullTypeName.ReflectionName;

        public string Name => this.FullTypeName.Name;

        public string ReflectionName => this.FullTypeName.ReflectionName;

        public string Namespace => this.FullTypeName.TopLevelTypeName.Namespace;

        public IReadOnlyList<IMember> Members => this.Methods.Cast<IMember>().Concat(this.Properties).Concat(this.Fields).Concat(this.Events).ToArray();
        public readonly List<IMethod> Methods = new List<IMethod>();
        IEnumerable<IMethod> ITypeDefinition.Methods => this.Methods;


        public readonly List<ITypeDefinition> NestedTypes = new List<ITypeDefinition>();
        IReadOnlyList<ITypeDefinition> ITypeDefinition.NestedTypes => this.NestedTypes;
        public readonly List<IField> Fields = new List<IField>();
        IEnumerable<IField> ITypeDefinition.Fields => this.Fields;

        public readonly List<IProperty> Properties = new List<IProperty>();
        IEnumerable<IProperty> ITypeDefinition.Properties => this.Properties;

        public readonly List<IEvent> Events = new List<IEvent>();
        IEnumerable<IEvent> ITypeDefinition.Events => this.Events;

        public KnownTypeCode KnownTypeCode => KnownTypeCode.None;

        public IType EnumUnderlyingType => throw new NotImplementedException();

        public bool IsReadOnly => false;

        public FullTypeName FullTypeName { get; }
        public bool HasExtensionMethods => false;

        public EntityHandle MetadataToken => default;


        public IModule ParentModule { get; }

        public Accessibility Accessibility { get; }

        public bool IsStatic { get; }

        public bool IsAbstract { get; }

        public bool IsSealed { get; }

        public SymbolKind SymbolKind => SymbolKind.TypeDefinition;

        public ICompilation Compilation => this.ParentModule.Compilation;

        public bool IsHidden { get; }

        public IType AcceptVisitor(TypeVisitor visitor)
        {
            return visitor.VisitTypeDefinition(this);
        }

        public bool Equals(IType other) => this.ReflectionName == other.ReflectionName;

        public override bool Equals(object other) => other is IType t && this.Equals(t);
        public override int GetHashCode() => this.ReflectionName.GetHashCode();

        public IEnumerable<IMethod> GetAccessors(Predicate<IMethod> filter = null, GetMemberOptions options = GetMemberOptions.None) => GetMethods(filter, options).Where(m => m.IsAccessor);

        public IEnumerable<IAttribute> GetAttributes()
        {
            yield break;
        }

        public IEnumerable<IMethod> GetConstructors(Predicate<IMethod> filter = null, GetMemberOptions options = GetMemberOptions.IgnoreInheritedMembers) => GetMethods(filter, options).Where(m => m.IsConstructor);

        public ITypeDefinition GetDefinition() => this;

        public IEnumerable<IEvent> GetEvents(Predicate<IEvent> filter = null, GetMemberOptions options = GetMemberOptions.None) =>
            this.Events.Where(e => filter?.Invoke(e) ?? true);

        public IEnumerable<IField> GetFields(Predicate<IField> filter = null, GetMemberOptions options = GetMemberOptions.None) =>
            this.Fields.Where(f => filter?.Invoke(f) ?? true);

        public IEnumerable<IMember> GetMembers(Predicate<IMember> filter = null, GetMemberOptions options = GetMemberOptions.None) =>
            this.Members.Where(m => filter?.Invoke(m) ?? true);

        public IEnumerable<IMethod> GetMethods(Predicate<IMethod> filter = null, GetMemberOptions options = GetMemberOptions.None) =>
            this.Methods.Where(a => filter?.Invoke(a) ?? true);

        public IEnumerable<IMethod> GetMethods(IReadOnlyList<IType> typeArguments, Predicate<IMethod> filter = null, GetMemberOptions options = GetMemberOptions.None) =>
            typeArguments.Count == 0 ? GetMethods(filter, options) : Enumerable.Empty<IMethod>(); // TODO generic methods
            //this.Methods.Where(m => filter?.Invoke(m) != false);

        public IEnumerable<IType> GetNestedTypes(Predicate<ITypeDefinition> filter = null, GetMemberOptions options = GetMemberOptions.None) =>
            this.NestedTypes.Where(t => filter?.Invoke(t) ?? true);


        public IEnumerable<IType> GetNestedTypes(IReadOnlyList<IType> typeArguments, Predicate<ITypeDefinition> filter = null, GetMemberOptions options = GetMemberOptions.None) =>
            typeArguments.Count == 0 ? GetNestedTypes(filter, options) : Enumerable.Empty<IType>(); // TODO generic types

        public IEnumerable<IProperty> GetProperties(Predicate<IProperty> filter = null, GetMemberOptions options = GetMemberOptions.None) =>
            Properties.Where(p => filter?.Invoke(p) != false);

        public TypeParameterSubstitution GetSubstitution()
        {
            return TypeParameterSubstitution.Identity;
        }

        public IType VisitChildren(TypeVisitor visitor) => this;

        public override string ToString() => ReflectionName;
    }
}
