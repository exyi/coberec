using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace Coberec.CSharpGen.TypeSystem
{
    public sealed class VirtualProperty : IProperty, IHideableMember
    {
        public VirtualProperty(ITypeDefinition declaringTypeDefinition, Accessibility accessibility, string name, IMethod getter, IMethod setter = null, bool isIndexer = false, bool isVirtual = false, bool isOverride = false, bool isStatic = false, bool isAbstract = false, bool isSealed = false, IReadOnlyList<IParameter> parameters = null, bool isHidden = false, IEnumerable<IMember> explicitImplementations = null)
        {
            var returnType = getter?.ReturnType ?? setter?.Parameters.Last().Type ?? throw new Exception($"Property {name} does not have getter nor setter");

            parameters = parameters ?? Array.Empty<IParameter>();

            if (isIndexer && parameters.Count == 0)
                throw new Exception("Indexers must have at least one parameter");

            if (getter != null && !getter.Parameters.Select(p => p.Type).SequenceEqual(parameters.Select(p => p.Type)))
                throw new Exception($"Getter has an unexpected signature");
            if (setter != null && !getter.Parameters.Select(p => p.Type).SequenceEqual(parameters.Select(p => p.Type).Append(returnType)))
                throw new Exception($"Setter has an unexpected signature.");

            if (getter is VirtualMethod getterM) getterM.AccessorOwner = this;
            if (setter is VirtualMethod setterM) setterM.AccessorOwner = this;

            this.Getter = getter;
            this.Setter = setter;
            this.IsIndexer = isIndexer;
            this.ReturnType = returnType;
            this.IsVirtual = isVirtual;
            this.IsOverride = isOverride;
            this.Name = name ?? throw new ArgumentNullException(nameof(name));
            this.DeclaringTypeDefinition = declaringTypeDefinition ?? throw new ArgumentNullException(nameof(declaringTypeDefinition));
            this.Accessibility = accessibility;
            this.IsStatic = isStatic;
            this.IsAbstract = isAbstract;
            this.IsSealed = isSealed;
            this.Parameters = parameters;
            this.IsHidden = isHidden;
            this.ExplicitlyImplementedInterfaceMembers = explicitImplementations?.ToArray() ?? Array.Empty<IMember>();
        }

        public bool CanGet => Getter != null;

        public bool CanSet => Setter != null;

        public IMethod Getter { get; }

        public IMethod Setter { get; }

        public bool IsIndexer { get; }

        public IReadOnlyList<IParameter> Parameters { get; }

        public IMember MemberDefinition => this;

        public IType ReturnType { get; }

        public IEnumerable<IMember> ExplicitlyImplementedInterfaceMembers { get; }

        public bool IsExplicitInterfaceImplementation => ExplicitlyImplementedInterfaceMembers.Count() > 0;

        public bool IsVirtual { get; }

        public bool IsOverride { get; }

        public bool IsOverridable => (this.IsAbstract || this.IsVirtual || this.IsOverride) && !this.IsSealed;

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

        public SymbolKind SymbolKind => this.IsIndexer ? SymbolKind.Indexer : SymbolKind.Property;

        public ICompilation Compilation => this.ParentModule.Compilation;

        public string FullName => $"{this.DeclaringTypeDefinition.FullName}.{this.Name}";

        public string ReflectionName => $"{this.DeclaringTypeDefinition.ReflectionName}.{this.Name}";

        public string Namespace => this.DeclaringTypeDefinition.Namespace;

        public bool IsHidden { get; }

        public bool Equals(IMember obj, TypeVisitor typeNormalization) =>
            this.Name == obj.Name &&
            this.DeclaringType.AcceptVisitor(typeNormalization).Equals(
                obj.DeclaringType.AcceptVisitor(typeNormalization));

        public readonly List<IAttribute> Attributes = new List<IAttribute>();
        public IEnumerable<IAttribute> GetAttributes() => Attributes;

        public IMember Specialize(TypeParameterSubstitution substitution)
        {
            throw new NotImplementedException();
        }
    }
}
