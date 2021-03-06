﻿// Copyright (c) 2014 Daniel Grunwald
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using ICSharpCode.Decompiler.CSharp.Resolver;
using ICSharpCode.Decompiler.CSharp.Syntax;
using ICSharpCode.Decompiler.CSharp.Transforms;
using ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.IL.ControlFlow;
using ICSharpCode.Decompiler.IL.Transforms;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.Semantics;
using ICSharpCode.Decompiler.Util;
using System.IO;
using ICSharpCode.Decompiler.CSharp.Syntax.PatternMatching;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Reflection.Metadata;
using SRM = System.Reflection.Metadata;
using ICSharpCode.Decompiler.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler.Documentation;
using ICSharpCode.Decompiler.DebugInfo;
using ICSharpCode.Decompiler.Disassembler;
using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Coberec.CSharpGen.TypeSystem;
using System.Threading.Tasks;

namespace Coberec.CSharpGen.Emit
{
    /// <summary>
    /// Main class of the C# decompiler engine.
    /// </summary>
    /// <remarks>
    /// Instances of this class are not thread-safe. Use separate instances to decompile multiple members in parallel.
    /// (in particular, the transform instances are not thread-safe)
    /// </remarks>
    public class CSharpEmitter
    {
        readonly ICompilation typeSystem;
        readonly IModule module;
        readonly DecompilerSettings settings;
        SyntaxTree syntaxTree;

        List<IILTransform> ilTransforms = CSharpDecompiler.GetILTransforms()
            // .Where(t => t.GetType().FullName != "ICSharpCode.Decompiler.IL.Transforms.SplitVariables")
            // .Where(t => t.GetType().FullName != "ICSharpCode.Decompiler.IL.Transforms.ILInlining")
            .Where(t => t.GetType().FullName != "ICSharpCode.Decompiler.IL.ControlFlow.YieldReturnDecompiler")
            .Where(t => t.GetType().FullName != "ICSharpCode.Decompiler.IL.Transforms.LocalFunctionDecompiler")
            .ToList();

        List<IAstTransform> astTransforms = CSharpDecompiler.GetAstTransforms();

        /// <summary>
        /// Token to check for requested cancellation of the decompilation.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// The type system created from the main module and referenced modules.
        /// </summary>
        public ICompilation TypeSystem => typeSystem;

        /// <summary>
        /// Gets or sets the optional provider for debug info.
        /// </summary>
        public IDebugInfoProvider DebugInfoProvider { get; set; }

        /// <summary>
        /// Gets or sets the optional provider for XML documentation strings.
        /// </summary>
        public IDocumentationProvider DocumentationProvider { get; set; }

        /// <summary>
        /// IL transforms.
        /// </summary>
        public IList<IILTransform> ILTransforms => ilTransforms;

        /// <summary>
        /// C# AST transforms.
        /// </summary>
        public IList<IAstTransform> AstTransforms
        {
            get { return astTransforms; }
        }

        private readonly bool usePartialClasses;

        /// <summary>
        /// Creates a new <see cref="CSharpEmitter"/> instance from the given <paramref name="typeSystem"/> and the given <paramref name="settings"/>.
        /// </summary>
        public CSharpEmitter(IDecompilerTypeSystem typeSystem, DecompilerSettings settings, bool usePartialClasses)
        {
            this.usePartialClasses = usePartialClasses;
            this.typeSystem = typeSystem ?? throw new ArgumentNullException(nameof(typeSystem));
            this.settings = settings;
            this.module = (typeSystem as ICompilation).MainModule;
        }

        public static bool MemberIsHidden(IModule module, IEntity member, DecompilerSettings settings) => (member as IHideableMember)?.IsHidden == true;

        // #region MemberIsHidden
        // public static bool MemberIsHidden(Metadata.PEFile module, EntityHandle member, DecompilerSettings settings)
        // {
        //     if (module == null || member.IsNil)
        //         return false;
        //     var metadata = module.Metadata;
        //     string name;
        //     switch (member.Kind)
        //     {
        //         case HandleKind.MethodDefinition:
        //             var methodHandle = (MethodDefinitionHandle)member;
        //             var method = metadata.GetMethodDefinition(methodHandle);
        //             var methodSemantics = module.MethodSemanticsLookup.GetSemantics(methodHandle).Item2;
        //             if (methodSemantics != 0 && methodSemantics != System.Reflection.MethodSemanticsAttributes.Other)
        //                 return true;
        //             if (LocalFunctionDecompiler.IsLocalFunctionMethod(module, methodHandle))
        //                 return settings.LocalFunctions;
        //             if (settings.AnonymousMethods && methodHandle.HasGeneratedName(metadata) && methodHandle.IsCompilerGenerated(metadata))
        //                 return true;
        //             if (settings.AsyncAwait && AsyncAwaitDecompiler.IsCompilerGeneratedMainMethod(module, methodHandle))
        //                 return true;
        //             return false;
        //         case HandleKind.TypeDefinition:
        //             var typeHandle = (TypeDefinitionHandle)member;
        //             var type = metadata.GetTypeDefinition(typeHandle);
        //             name = metadata.GetString(type.Name);
        //             if (!type.GetDeclaringType().IsNil)
        //             {
        //                 if (LocalFunctionDecompiler.IsLocalFunctionDisplayClass(module, typeHandle))
        //                     return settings.LocalFunctions;
        //                 if (settings.AnonymousMethods && IsClosureType(type, metadata))
        //                     return true;
        //                 if (settings.YieldReturn && YieldReturnDecompiler.IsCompilerGeneratorEnumerator(typeHandle, metadata))
        //                     return true;
        //                 if (settings.AsyncAwait && AsyncAwaitDecompiler.IsCompilerGeneratedStateMachine(typeHandle, metadata))
        //                     return true;
        //                 if (settings.FixedBuffers && name.StartsWith("<", StringComparison.Ordinal) && name.Contains("__FixedBuffer"))
        //                     return true;
        //             }
        //             else if (type.IsCompilerGenerated(metadata))
        //             {
        //                 if (settings.ArrayInitializers && name.StartsWith("<PrivateImplementationDetails>", StringComparison.Ordinal))
        //                     return true;
        //                 if (settings.AnonymousTypes && type.IsAnonymousType(metadata))
        //                     return true;
        //                 if (settings.Dynamic && type.IsDelegate(metadata) && (name.StartsWith("<>A", StringComparison.Ordinal) || name.StartsWith("<>F", StringComparison.Ordinal)))
        //                     return true;
        //             }
        //             if (settings.ArrayInitializers && settings.SwitchStatementOnString && name.StartsWith("<PrivateImplementationDetails>", StringComparison.Ordinal))
        //                 return true;
        //             return false;
        //         case HandleKind.FieldDefinition:
        //             var fieldHandle = (FieldDefinitionHandle)member;
        //             var field = metadata.GetFieldDefinition(fieldHandle);
        //             name = metadata.GetString(field.Name);
        //             if (field.IsCompilerGenerated(metadata))
        //             {
        //                 if (settings.AnonymousMethods && IsAnonymousMethodCacheField(field, metadata))
        //                     return true;
        //                 if (settings.AutomaticProperties && IsAutomaticPropertyBackingField(field, metadata))
        //                     return true;
        //                 if (settings.SwitchStatementOnString && IsSwitchOnStringCache(field, metadata))
        //                     return true;
        //             }
        //             // event-fields are not [CompilerGenerated]
        //             if (settings.AutomaticEvents && metadata.GetTypeDefinition(field.GetDeclaringType()).GetEvents().Any(ev => metadata.GetEventDefinition(ev).Name == field.Name))
        //                 return true;
        //             if (settings.ArrayInitializers && metadata.GetString(metadata.GetTypeDefinition(field.GetDeclaringType()).Name).StartsWith("<PrivateImplementationDetails>", StringComparison.Ordinal))
        //             {
        //                 // only hide fields starting with '__StaticArrayInit'
        //                 if (name.StartsWith("__StaticArrayInit", StringComparison.Ordinal))
        //                     return true;
        //                 // hide fields starting with '$$method'
        //                 if (name.StartsWith("$$method", StringComparison.Ordinal))
        //                     return true;
        //                 if (field.DecodeSignature(new Metadata.FullTypeNameSignatureDecoder(metadata), default).ToString().StartsWith("__StaticArrayInit", StringComparison.Ordinal))
        //                     return true;
        //             }
        //             return false;
        //     }

        //     return false;
        // }

        // static bool IsSwitchOnStringCache(SRM.FieldDefinition field, MetadataReader metadata)
        // {
        //     return metadata.GetString(field.Name).StartsWith("<>f__switch", StringComparison.Ordinal);
        // }

        // static bool IsAutomaticPropertyBackingField(SRM.FieldDefinition field, MetadataReader metadata)
        // {
        //     var name = metadata.GetString(field.Name);
        //     return name.StartsWith("<", StringComparison.Ordinal) && name.EndsWith("BackingField", StringComparison.Ordinal);
        // }

        // static bool IsAnonymousMethodCacheField(SRM.FieldDefinition field, MetadataReader metadata)
        // {
        //     var name = metadata.GetString(field.Name);
        //     return name.StartsWith("CS$<>", StringComparison.Ordinal) || name.StartsWith("<>f__am", StringComparison.Ordinal);
        // }

        // static bool IsClosureType(SRM.TypeDefinition type, MetadataReader metadata)
        // {
        //     var name = metadata.GetString(type.Name);
        //     if (!type.Name.IsGeneratedName(metadata) || !type.IsCompilerGenerated(metadata))
        //         return false;
        //     if (name.Contains("DisplayClass") || name.Contains("AnonStorey"))
        //         return true;
        //     return type.BaseType.GetFullTypeName(metadata).ToString() == "System.Object" && !type.GetInterfaceImplementations().Any();
        // }
        // #endregion


        TypeSystemAstBuilder CreateAstBuilder(ITypeResolveContext decompilationContext)
        {
            var typeSystemAstBuilder = new TypeSystemAstBuilder();
            typeSystemAstBuilder.ShowAttributes = true;
            typeSystemAstBuilder.AlwaysUseShortTypeNames = true;
            typeSystemAstBuilder.AddResolveResultAnnotations = true;
            return typeSystemAstBuilder;
        }

        void RunTransforms(AstNode rootNode, DecompileRun decompileRun, ITypeResolveContext decompilationContext)
        {
            var typeSystemAstBuilder = CreateAstBuilder(decompilationContext);
            var context = new TransformContext((IDecompilerTypeSystem)typeSystem, decompileRun, decompilationContext, typeSystemAstBuilder);
            // Debug.Assert(context.RequiredNamespacesSuperset.Count > 0);
            foreach (var transform in astTransforms)
            {
                CancellationToken.ThrowIfCancellationRequested();
                transform.Run(rootNode, context);
            }
            rootNode.AcceptVisitor(new InsertParenthesesVisitor { InsertParenthesesForReadability = true });
        }

        string SyntaxTreeToString(SyntaxTree syntaxTree)
        {
            StringWriter w = new StringWriter();
            syntaxTree.AcceptVisitor(new CSharpOutputVisitor(w, settings.CSharpFormattingOptions));
            return w.ToString();
        }

        /// <summary>
        /// Decompile assembly and module attributes.
        /// </summary>
        public SyntaxTree DecompileModuleAndAssemblyAttributes()
        {
            var decompilationContext = new SimpleTypeResolveContext(typeSystem.MainModule);
            var decompileRun = new DecompileRun(settings)
            {
                DocumentationProvider = DocumentationProvider,
                CancellationToken = CancellationToken
            };
            syntaxTree = new SyntaxTree();
            RequiredNamespaceCollector_Hacked.CollectAttributeNamespaces(module, decompileRun.Namespaces);
            DoDecompileModuleAndAssemblyAttributes(decompileRun, decompilationContext, syntaxTree);
            RunTransforms(syntaxTree, decompileRun, decompilationContext);
            return syntaxTree;
        }

        /// <summary>
        /// Decompile assembly and module attributes.
        /// </summary>
        public string DecompileModuleAndAssemblyAttributesToString()
        {
            return SyntaxTreeToString(DecompileModuleAndAssemblyAttributes());
        }

        void DoDecompileModuleAndAssemblyAttributes(DecompileRun decompileRun, ITypeResolveContext decompilationContext, SyntaxTree syntaxTree)
        {
            try
            {
                foreach (var a in typeSystem.MainModule.GetAssemblyAttributes())
                {
                    var astBuilder = CreateAstBuilder(decompilationContext);
                    var attrSection = new AttributeSection(astBuilder.ConvertAttribute(a));
                    attrSection.AttributeTarget = "assembly";
                    syntaxTree.AddChild(attrSection, SyntaxTree.MemberRole);
                }
                foreach (var a in typeSystem.MainModule.GetModuleAttributes())
                {
                    var astBuilder = CreateAstBuilder(decompilationContext);
                    var attrSection = new AttributeSection(astBuilder.ConvertAttribute(a));
                    attrSection.AttributeTarget = "module";
                    syntaxTree.AddChild(attrSection, SyntaxTree.MemberRole);
                }
            }
            catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException)) {
               throw new DecompilerException(this.module, null, innerException, "Error decompiling module and assembly attributes of " + module.AssemblyName);
            }
        }

        void DoDecompileTypes(IEnumerable<IType> types, DecompileRun decompileRun, ITypeResolveContext decompilationContext, SyntaxTree syntaxTree)
        {
            string currentNamespace = null;
            AstNode groupNode = null;
            foreach (var type in types)
            {
                var typeDef = type.GetDefinition();
                if (typeDef.Name == "<Module>" && typeDef.Members.Count == 0)
                    continue;
                if (MemberIsHidden(module, typeDef, settings))
                    continue;
                if (string.IsNullOrEmpty(typeDef.Namespace))
                {
                    groupNode = syntaxTree;
                }
                else
                {
                    if (currentNamespace != typeDef.Namespace)
                    {
                        groupNode = new NamespaceDeclaration(typeDef.Namespace);
                        syntaxTree.AddChild(groupNode, SyntaxTree.MemberRole);
                    }
                }
                currentNamespace = typeDef.Namespace;
                var typeDecl = DoDecompile(typeDef, decompileRun, decompilationContext.WithCurrentTypeDefinition(typeDef));
                groupNode.AddChild(typeDecl, SyntaxTree.MemberRole);
            }
        }

        /// <summary>
        /// Decompiles the whole module into a single syntax tree.
        /// </summary>
        public SyntaxTree DecompileWholeModuleAsSingleFile()
        {
            return DecompileWholeModuleAsSingleFile(false);
        }

        /// <summary>
        /// Decompiles the whole module into a single syntax tree.
        /// </summary>
        /// <param name="sortTypes">If true, top-level-types are emitted sorted by namespace/name.
        /// If false, types are emitted in metadata order.</param>
        public SyntaxTree DecompileWholeModuleAsSingleFile(bool sortTypes)
        {
            var decompilationContext = new SimpleTypeResolveContext(typeSystem.MainModule);
            var decompileRun = new DecompileRun(settings)
            {
                DocumentationProvider = DocumentationProvider,
                CancellationToken = CancellationToken
            };
            syntaxTree = new SyntaxTree();
            RequiredNamespaceCollector_Hacked.CollectNamespaces(module, decompileRun.Namespaces);
            DoDecompileModuleAndAssemblyAttributes(decompileRun, decompilationContext, syntaxTree);
            var typeDefs = module.TopLevelTypeDefinitions;
            if (sortTypes) {
                typeDefs = typeDefs.OrderBy(typeDef => {
                    return (typeDef.Namespace, typeDef.Name);
                });
            }
            DoDecompileTypes(typeDefs, decompileRun, decompilationContext, syntaxTree);
            RunTransforms(syntaxTree, decompileRun, decompilationContext);
            return syntaxTree;
        }

        /// <summary>
        /// Creates an <see cref="ILTransformContext"/> for the given <paramref name="function"/>.
        /// </summary>
        public ILTransformContext CreateILTransformContext(ILFunction function)
        {
            var decompileRun = new DecompileRun(settings)
            {
                DocumentationProvider = DocumentationProvider,
                CancellationToken = CancellationToken
            };
            RequiredNamespaceCollector_Hacked.CollectNamespaces(function.Method, module, decompileRun.Namespaces);
            return new ILTransformContext(function, (IDecompilerTypeSystem)typeSystem, DebugInfoProvider, settings)
            {
                CancellationToken = CancellationToken,
                DecompileRun = decompileRun
            };
        }

        /// <summary>
        /// Decompiles the whole module into a single string.
        /// </summary>
        public string DecompileWholeModuleAsString()
        {
            return SyntaxTreeToString(DecompileWholeModuleAsSingleFile());
        }

        /// <summary>
        /// Decompile the given types.
        /// </summary>
        /// <remarks>
        /// Unlike Decompile(IMemberDefinition[]), this method will add namespace declarations around the type definitions.
        /// </remarks>
        public SyntaxTree DecompileTypes(IEnumerable<ITypeDefinition> types)
        {
            if (types == null)
                throw new ArgumentNullException(nameof(types));
            var decompilationContext = new SimpleTypeResolveContext(typeSystem.MainModule);
            var decompileRun = new DecompileRun(settings)
            {
                DocumentationProvider = DocumentationProvider,
                CancellationToken = CancellationToken
            };
            syntaxTree = new SyntaxTree();

            foreach (var type in types)
            {
                RequiredNamespaceCollector_Hacked.CollectNamespaces(type, module, decompileRun.Namespaces);
            }

            DoDecompileTypes(types, decompileRun, decompilationContext, syntaxTree);
            RunTransforms(syntaxTree, decompileRun, decompilationContext);
            return syntaxTree;
        }

        protected virtual bool IncludeTypeWhenDecompilingProject(ITypeDefinition type)
        {
            if (type.Name == "<Module>" || type is IHideableMember hideable && hideable.IsHidden || MemberIsHidden(module, type, settings))
                return false;
            return true;
        }

        public IEnumerable<string> WriteCodeFilesInProject(string targetDirectory)
        {
            var files = this.typeSystem.MainModule.TopLevelTypeDefinitions.Where(IncludeTypeWhenDecompilingProject).GroupBy(type => {
                string file = WholeProjectDecompiler.CleanUpFileName(type.Name + ".cs");
                return file;
            }, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var file in files)
            {
                using (StreamWriter w = new StreamWriter(Path.Combine(targetDirectory, file.Key))) {
                    var syntaxTree = DecompileTypes(file.ToArray());
                    syntaxTree.AcceptVisitor(new CSharpOutputVisitor(w, settings.CSharpFormattingOptions));
                }
            }
            return files.Select(f => f.Key);
        }

        /// <summary>
        /// Decompile the given types.
        /// </summary>
        /// <remarks>
        /// Unlike Decompile(IMemberDefinition[]), this method will add namespace declarations around the type definitions.
        /// </remarks>
        public string DecompileTypesAsString(IEnumerable<ITypeDefinition> types)
        {
            return SyntaxTreeToString(DecompileTypes(types));
        }

        /// <summary>
        /// Decompile the given type.
        /// </summary>
        /// <remarks>
        /// Unlike Decompile(IMemberDefinition[]), this method will add namespace declarations around the type definition.
        /// </remarks>
        public SyntaxTree DecompileType(FullTypeName fullTypeName)
        {
            var type = typeSystem.FindType(fullTypeName.TopLevelTypeName).GetDefinition();
            if (type == null)
                throw new InvalidOperationException($"Could not find type definition {fullTypeName} in type system.");
            var decompilationContext = new SimpleTypeResolveContext(typeSystem.MainModule);
            var decompileRun = new DecompileRun(settings)
            {
                DocumentationProvider = DocumentationProvider,
                CancellationToken = CancellationToken
            };
            syntaxTree = new SyntaxTree();
            RequiredNamespaceCollector_Hacked.CollectNamespaces(type, module, decompileRun.Namespaces);
            DoDecompileTypes(new[] { type }, decompileRun, decompilationContext, syntaxTree);
            RunTransforms(syntaxTree, decompileRun, decompilationContext);
            return syntaxTree;
        }

        /// <summary>
        /// Decompile the given type.
        /// </summary>
        /// <remarks>
        /// Unlike Decompile(IMemberDefinition[]), this method will add namespace declarations around the type definition.
        /// </remarks>
        public string DecompileTypeAsString(FullTypeName fullTypeName)
        {
            return SyntaxTreeToString(DecompileType(fullTypeName));
        }

        /// <summary>
        /// Decompile the specified types and/or members.
        /// </summary>
        public SyntaxTree Decompile(params IEntity[] definitions)
        {
            return Decompile((IEnumerable<IEntity>)definitions);
        }

        /// <summary>
        /// Decompile the specified types and/or members.
        /// </summary>
        public SyntaxTree Decompile(IEnumerable<IEntity> definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));
            syntaxTree = new SyntaxTree();
            var decompileRun = new DecompileRun(settings)
            {
                DocumentationProvider = DocumentationProvider,
                CancellationToken = CancellationToken
            };
            foreach (var entity in definitions)
            {
                RequiredNamespaceCollector_Hacked.CollectNamespaces(entity, module, decompileRun.Namespaces);
            }

            bool first = true;
            ITypeDefinition parentTypeDef = null;

            foreach (var entity in definitions)
            {
                switch (entity)
                {
                    case ITypeDefinition typeDef:
                        syntaxTree.Members.Add(DoDecompile(typeDef, decompileRun, new SimpleTypeResolveContext(typeDef)));
                        if (first)
                        {
                            parentTypeDef = typeDef.DeclaringTypeDefinition;
                        }
                        else if (parentTypeDef != null)
                        {
                            parentTypeDef = FindCommonDeclaringTypeDefinition(parentTypeDef, typeDef.DeclaringTypeDefinition);
                        }
                        break;
                    case IMethod method:
                        syntaxTree.Members.Add(DoDecompile(method, decompileRun, new SimpleTypeResolveContext(method)));
                        if (first)
                        {
                            parentTypeDef = method.DeclaringTypeDefinition;
                        }
                        else if (parentTypeDef != null)
                        {
                            parentTypeDef = FindCommonDeclaringTypeDefinition(parentTypeDef, method.DeclaringTypeDefinition);
                        }
                        break;
                    case IField field:
                        syntaxTree.Members.Add(DoDecompile(field, decompileRun, new SimpleTypeResolveContext(field)));
                        parentTypeDef = field.DeclaringTypeDefinition;
                        break;
                    case IProperty property:
                        syntaxTree.Members.Add(DoDecompile(property, decompileRun, new SimpleTypeResolveContext(property)));
                        if (first)
                        {
                            parentTypeDef = property.DeclaringTypeDefinition;
                        }
                        else if (parentTypeDef != null)
                        {
                            parentTypeDef = FindCommonDeclaringTypeDefinition(parentTypeDef, property.DeclaringTypeDefinition);
                        }
                        break;
                    case IEvent ev:
                        syntaxTree.Members.Add(DoDecompile(ev, decompileRun, new SimpleTypeResolveContext(ev)));
                        if (first)
                        {
                            parentTypeDef = ev.DeclaringTypeDefinition;
                        }
                        else if (parentTypeDef != null)
                        {
                            parentTypeDef = FindCommonDeclaringTypeDefinition(parentTypeDef, ev.DeclaringTypeDefinition);
                        }
                        break;
                    default:
                        throw new NotSupportedException(entity.ToString());
                }
                first = false;
            }
            RunTransforms(syntaxTree, decompileRun, parentTypeDef != null ? new SimpleTypeResolveContext(parentTypeDef) : new SimpleTypeResolveContext(typeSystem.MainModule));
            return syntaxTree;
        }

        ITypeDefinition FindCommonDeclaringTypeDefinition(ITypeDefinition a, ITypeDefinition b)
        {
            if (a == null || b == null)
                return null;
            var declaringTypes = a.GetDeclaringTypeDefinitions();
            var set = new HashSet<ITypeDefinition>(b.GetDeclaringTypeDefinitions());
            return declaringTypes.FirstOrDefault(set.Contains);
        }

        /// <summary>
        /// Decompile the specified types and/or members.
        /// </summary>
        public string DecompileAsString(params IEntity[] definitions)
        {
            return SyntaxTreeToString(Decompile(definitions));
        }

        /// <summary>
        /// Decompile the specified types and/or members.
        /// </summary>
        public string DecompileAsString(IEnumerable<IEntity> definitions)
        {
            return SyntaxTreeToString(Decompile(definitions));
        }


        // TODO: get implementations of a method
        // IEnumerable<EntityDeclaration> AddInterfaceImplHelpers(
        //     EntityDeclaration memberDecl, IMethod method,
        //     TypeSystemAstBuilder astBuilder)
        // {
        //     if (!memberDecl.GetChildByRole(EntityDeclaration.PrivateImplementationTypeRole).IsNull)
        //     {
        //         yield break; // cannot create forwarder for existing explicit interface impl
        //     }
        //     var genericContext = new Decompiler.TypeSystem.GenericContext(method);
        //     var methodHandle = (MethodDefinitionHandle)method.MetadataToken;
        //     foreach (var h in methodHandle.GetMethodImplementations(metadata))
        //     {
        //         var mi = metadata.GetMethodImplementation(h);
        //         IMethod m = module.ResolveMethod(mi.MethodDeclaration, genericContext);
        //         if (m == null || m.DeclaringType.Kind != TypeKind.Interface)
        //             continue;
        //         var methodDecl = new MethodDeclaration();
        //         methodDecl.ReturnType = memberDecl.ReturnType.Clone();
        //         methodDecl.PrivateImplementationType = astBuilder.ConvertType(m.DeclaringType);
        //         methodDecl.Name = m.Name;
        //         methodDecl.TypeParameters.AddRange(memberDecl.GetChildrenByRole(Roles.TypeParameter)
        //                                            .Select(n => (TypeParameterDeclaration)n.Clone()));
        //         methodDecl.Parameters.AddRange(memberDecl.GetChildrenByRole(Roles.Parameter).Select(n => n.Clone()));
        //         methodDecl.Constraints.AddRange(memberDecl.GetChildrenByRole(Roles.Constraint)
        //                                         .Select(n => (Constraint)n.Clone()));

        //         methodDecl.Body = new BlockStatement();
        //         methodDecl.Body.AddChild(new Comment(
        //             "ILSpy generated this explicit interface implementation from .override directive in " + memberDecl.Name),
        //                                  Roles.Comment);
        //         var forwardingCall = new InvocationExpression(new MemberReferenceExpression(new ThisReferenceExpression(), memberDecl.Name,
        //             methodDecl.TypeParameters.Select(tp => new SimpleType(tp.Name))),
        //             methodDecl.Parameters.Select(p => ForwardParameter(p))
        //         );
        //         if (m.ReturnType.IsKnownType(KnownTypeCode.Void))
        //         {
        //             methodDecl.Body.Add(new ExpressionStatement(forwardingCall));
        //         }
        //         else
        //         {
        //             methodDecl.Body.Add(new ReturnStatement(forwardingCall));
        //         }
        //         yield return methodDecl;
        //     }
        // }

        Expression ForwardParameter(ParameterDeclaration p)
        {
            switch (p.ParameterModifier)
            {
                case ParameterModifier.Ref:
                    return new DirectionExpression(FieldDirection.Ref, new IdentifierExpression(p.Name));
                case ParameterModifier.Out:
                    return new DirectionExpression(FieldDirection.Out, new IdentifierExpression(p.Name));
                default:
                    return new IdentifierExpression(p.Name);
            }
        }

        /// <summary>
        /// Sets new modifier if the member hides some other member from a base type.
        /// </summary>
        /// <param name="member">The node of the member which new modifier state should be determined.</param>
        void SetNewModifier(EntityDeclaration member)
        {
            var entity = (IEntity)member.GetSymbol();
            var lookup = new MemberLookup(entity.DeclaringTypeDefinition, entity.ParentModule);

            var baseTypes = entity.DeclaringType.GetNonInterfaceBaseTypes().Where(t => entity.DeclaringType != t).ToList();

            // A constant, field, property, event, or type introduced in a class or struct hides all base class members with the same name.
            bool hideBasedOnSignature = !(entity is ITypeDefinition
                    || entity.SymbolKind == SymbolKind.Field
                    || entity.SymbolKind == SymbolKind.Property
                    || entity.SymbolKind == SymbolKind.Event);

            const GetMemberOptions options = GetMemberOptions.IgnoreInheritedMembers | GetMemberOptions.ReturnMemberDefinitions;

            if (HidesMemberOrTypeOfBaseType())
                member.Modifiers |= Modifiers.New;

            bool HidesMemberOrTypeOfBaseType()
            {
                var parameterListComparer = ParameterListComparer.WithOptions(includeModifiers: true);

                foreach (IType baseType in baseTypes) {
                    if (!hideBasedOnSignature) {
                        if (baseType.GetNestedTypes(t => t.Name == entity.Name && lookup.IsAccessible(t, true), options).Any())
                            return true;
                        if (baseType.GetMembers(m => m.Name == entity.Name && m.SymbolKind != SymbolKind.Indexer && lookup.IsAccessible(m, true), options).Any())
                            return true;
                    } else {
                        if (entity.SymbolKind == SymbolKind.Indexer) {
                            // An indexer introduced in a class or struct hides all base class indexers with the same signature (parameter count and types).
                            if (baseType.GetProperties(p => p.SymbolKind == SymbolKind.Indexer && lookup.IsAccessible(p, true))
                                        .Any(p => parameterListComparer.Equals(((IProperty)entity).Parameters, p.Parameters)))
                            {
                                return true;
                            }
                        } else if (entity.SymbolKind == SymbolKind.Method) {
                            // A method introduced in a class or struct hides all non-method base class members with the same name, and all
                            // base class methods with the same signature (method name and parameter count, modifiers, and types).
                            if (baseType.GetMembers(m => m.SymbolKind != SymbolKind.Indexer && m.Name == entity.Name && lookup.IsAccessible(m, true))
                                        .Any(m => m.SymbolKind != SymbolKind.Method || (((IMethod)entity).TypeParameters.Count == ((IMethod)m).TypeParameters.Count
                                                                                                                                        && parameterListComparer.Equals(((IMethod)entity).Parameters, ((IMethod)m).Parameters))))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
        }

        void FixParameterNames(EntityDeclaration entity)
        {
            int i = 0;
            foreach (var parameter in entity.GetChildrenByRole(Roles.Parameter))
            {
                if (string.IsNullOrEmpty(parameter.Name) && !parameter.Type.IsArgList())
                {
                    // needs to be consistent with logic in ILReader.CreateILVarable(ParameterDefinition)
                    parameter.Name = "P_" + i;
                }
                i++;
            }
        }

        protected EntityDeclaration DoDecompile(ITypeDefinition typeDef, DecompileRun decompileRun, ITypeResolveContext decompilationContext)
        {
            Debug.Assert(decompilationContext.CurrentTypeDefinition == typeDef);
            try
            {
            var typeSystemAstBuilder = CreateAstBuilder(decompilationContext);
            var entityDecl = typeSystemAstBuilder.ConvertEntity(typeDef);
            if (this.usePartialClasses)
                entityDecl.Modifiers |= Modifiers.Partial;
            var typeDecl = entityDecl as TypeDeclaration;
            if (typeDecl == null)
            {
                // e.g. DelegateDeclaration
                return entityDecl;
            }
            foreach (var type in typeDef.NestedTypes)
            {
                if (!MemberIsHidden(module, type, settings))
                {
                    var nestedType = DoDecompile(type, decompileRun, decompilationContext.WithCurrentTypeDefinition(type));
                    SetNewModifier(nestedType);
                    typeDecl.Members.Add(nestedType);
                }
            }
            foreach (var field in typeDef.Fields)
            {
                if (!MemberIsHidden(module, field, settings))
                {
                    if (typeDef.Kind == TypeKind.Enum && !field.IsConst)
                        continue;
                    var memberDecl = DoDecompile(field, decompileRun, decompilationContext.WithCurrentMember(field));
                    typeDecl.Members.Add(memberDecl);
                }
            }
            foreach (var property in typeDef.Properties)
            {
                if (!MemberIsHidden(module, property, settings))
                {
                    var propDecl = DoDecompile(property, decompileRun, decompilationContext.WithCurrentMember(property));
                    typeDecl.Members.Add(propDecl);
                }
            }
            foreach (var @event in typeDef.Events)
            {
                if (!MemberIsHidden(module, @event, settings))
                {
                    var eventDecl = DoDecompile(@event, decompileRun, decompilationContext.WithCurrentMember(@event));
                    typeDecl.Members.Add(eventDecl);
                }
            }
            foreach (var method in typeDef.Methods)
            {
                if (!MemberIsHidden(module, method, settings))
                {
                    var memberDecl = DoDecompile(method, decompileRun, decompilationContext.WithCurrentMember(method));
                    typeDecl.Members.Add(memberDecl);
                    // typeDecl.Members.AddRange(AddInterfaceImplHelpers(memberDecl, method, typeSystemAstBuilder));
                }
            }
            if (typeDecl.Members.OfType<IndexerDeclaration>().Any(idx => idx.PrivateImplementationType.IsNull))
            {
                // Remove the [DefaultMember] attribute if the class contains indexers
                RemoveAttribute(typeDecl, KnownAttribute.DefaultMember);
            }
            if (settings.IntroduceRefModifiersOnStructs)
            {
                if (FindAttribute(typeDecl, KnownAttribute.Obsolete, out var attr))
                {
                    if (obsoleteAttributePattern.IsMatch(attr))
                    {
                        if (attr.Parent is AttributeSection section && section.Attributes.Count == 1)
                            section.Remove();
                        else
                            attr.Remove();
                    }
                }
            }
            if (typeDecl.ClassType == ClassType.Enum)
            {
                switch (DetectBestEnumValueDisplayMode(typeDef, module))
                {
                    case EnumValueDisplayMode.FirstOnly:
                        foreach (var enumMember in typeDecl.Members.OfType<EnumMemberDeclaration>().Skip(1))
                        {
                            enumMember.Initializer = null;
                        }
                        break;
                    case EnumValueDisplayMode.None:
                        foreach (var enumMember in typeDecl.Members.OfType<EnumMemberDeclaration>())
                        {
                            enumMember.Initializer = null;
                        }
                        break;
                    case EnumValueDisplayMode.All:
                        // nothing needs to be changed.
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return typeDecl;
            } catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException)) {
                throw new DecompilerException(module, typeDef, innerException);
            }
        }

        enum EnumValueDisplayMode
        {
            None,
            All,
            FirstOnly
        }

        EnumValueDisplayMode DetectBestEnumValueDisplayMode(ITypeDefinition typeDef, IModule module)
        {
            if (typeDef.HasAttribute(KnownAttribute.Flags, inherit: false))
                return EnumValueDisplayMode.All;
            bool first = true;
            long firstValue = 0, previousValue = 0;
            foreach (var field in typeDef.Fields)
            {
                if (MemberIsHidden(module, field, settings))
                    continue;
                object constantValue = field.GetConstantValue();
                if (constantValue == null)
                    continue;
                long currentValue = (long)CSharpPrimitiveCast.Cast(TypeCode.Int64, constantValue, false);
                if (first)
                {
                    firstValue = currentValue;
                    first = false;
                }
                else if (previousValue + 1 != currentValue)
                {
                    return EnumValueDisplayMode.All;
                }
                previousValue = currentValue;
            }
            return firstValue == 0 ? EnumValueDisplayMode.None : EnumValueDisplayMode.FirstOnly;
        }

        static readonly ICSharpCode.Decompiler.CSharp.Syntax.Attribute obsoleteAttributePattern = new ICSharpCode.Decompiler.CSharp.Syntax.Attribute()
        {
            Type = new TypePattern(typeof(ObsoleteAttribute)),
            Arguments = {
                new PrimitiveExpression("Types with embedded references are not supported in this version of your compiler."),
                new Choice() { new PrimitiveExpression(true), new PrimitiveExpression(false) }
            }
        };

        MethodDeclaration GenerateConvHelper(string name, KnownTypeCode source, KnownTypeCode target, TypeSystemAstBuilder typeSystemAstBuilder,
                                             Expression intermediate32, Expression intermediate64)
        {
            MethodDeclaration method = new MethodDeclaration();
            method.Name = name;
            method.Modifiers = Modifiers.Private | Modifiers.Static;
            method.Parameters.Add(new ParameterDeclaration(typeSystemAstBuilder.ConvertType(typeSystem.FindType(source)), "input"));
            method.ReturnType = typeSystemAstBuilder.ConvertType(typeSystem.FindType(target));
            method.Body = new BlockStatement {
                new IfElseStatement {
                    Condition = new BinaryOperatorExpression {
                        Left = new MemberReferenceExpression(new TypeReferenceExpression(typeSystemAstBuilder.ConvertType(typeSystem.FindType(KnownTypeCode.IntPtr))), "Size"),
                        Operator = BinaryOperatorType.Equality,
                        Right = new PrimitiveExpression(4)
                    },
                    TrueStatement = new BlockStatement { // 32-bit
                        new ReturnStatement(
                            new CastExpression(
                                method.ReturnType.Clone(),
                                intermediate32
                            )
                        )
                    },
                    FalseStatement = new BlockStatement { // 64-bit
                        new ReturnStatement(
                            new CastExpression(
                                method.ReturnType.Clone(),
                                intermediate64
                            )
                        )
                    },
                }
            };
            return method;
        }

        protected EntityDeclaration DoDecompile(IMethod method, DecompileRun decompileRun, ITypeResolveContext decompilationContext)
        {
            Debug.Assert(decompilationContext.CurrentMember == method);
            var typeSystemAstBuilder = CreateAstBuilder(decompilationContext);
            var methodDecl = typeSystemAstBuilder.ConvertEntity(method);
            int lastDot = method.Name.LastIndexOf('.');
            if (method.IsExplicitInterfaceImplementation && lastDot >= 0)
            {
                methodDecl.Name = method.Name.Substring(lastDot + 1);
            }
            FixParameterNames(methodDecl);

            if (method.HasBody)
            {
                DecompileBody(method, methodDecl, decompileRun, decompilationContext);
            }
            else if (!method.IsAbstract && method.DeclaringType.Kind != TypeKind.Interface)
            {
                if ((method as VirtualMethod)?.IsPartial == true)
                {
                    methodDecl.Modifiers |= Modifiers.Partial;
                    methodDecl.Modifiers &= ~(Modifiers.Private | Modifiers.Public | Modifiers.Private | Modifiers.Protected | Modifiers.Internal);
                }
                else
                    methodDecl.Modifiers |= Modifiers.Extern;
            }
            if (method.SymbolKind == SymbolKind.Method && !method.IsExplicitInterfaceImplementation && (method as IMethodWithDefinition)?.HasFlag(System.Reflection.MethodAttributes.Virtual) == (method as IMethodWithDefinition)?.HasFlag(System.Reflection.MethodAttributes.NewSlot))
            {
                SetNewModifier(methodDecl);
            }
            return methodDecl;
        }

        internal static bool IsWindowsFormsInitializeComponentMethod(IMethod method)
        {
            return method.ReturnType.Kind == TypeKind.Void && method.Name == "InitializeComponent" && method.DeclaringTypeDefinition.GetNonInterfaceBaseTypes().Any(t => t.FullName == "System.Windows.Forms.Control");
        }

        void DecompileBody(IMethod method, EntityDeclaration entityDecl, DecompileRun decompileRun, ITypeResolveContext decompilationContext)
        {
            try
            {
                var body = BlockStatement.Null;
                ILFunction function = (method as IMethodWithDefinition)?.GetBody() ?? throw new InvalidOperationException($"Method {method.FullName} does not have body.");
                function.CheckInvariantPublic(ILPhase.Normal);

                if (entityDecl != null)
                {
                    int i = 0;
                    var parameters = function.Variables.Where(v => v.Kind == VariableKind.Parameter).ToDictionary(v => v.Index);
                    foreach (var parameter in entityDecl.GetChildrenByRole(Roles.Parameter))
                    {
                        if (parameters.TryGetValue(i, out var v))
                            parameter.AddAnnotation(new ILVariableResolveResult(v, method.Parameters[i].Type));
                        i++;
                    }
                }

                var localSettings = settings.Clone();
                if (IsWindowsFormsInitializeComponentMethod(method))
                {
                    localSettings.UseImplicitMethodGroupConversion = false;
                    localSettings.UsingDeclarations = false;
                    localSettings.AlwaysCastTargetsOfExplicitInterfaceImplementationCalls = true;
                    localSettings.NamedArguments = false;
                }

                var context = new ILTransformContext(function, (IDecompilerTypeSystem)typeSystem, DebugInfoProvider, localSettings)
                {
                    CancellationToken = CancellationToken,
                    DecompileRun = decompileRun
                };
                foreach (var transform in ilTransforms)
                {
                    CancellationToken.ThrowIfCancellationRequested();
                    transform.Run(function, context);
                    function.CheckInvariantPublic(ILPhase.Normal);
                    // When decompiling definitions only, we can cancel decompilation of all steps
                    // after yield and async detection, because only those are needed to properly set
                    // IsAsync/IsIterator flags on ILFunction.
                    if (!localSettings.DecompileMemberBodies && transform is AsyncAwaitDecompiler)
                        break;
                }

                // Generate C# AST only if bodies should be displayed.
                if (localSettings.DecompileMemberBodies)
                {
                    AddDefinesForConditionalAttributes(function, decompileRun);
                    var statementBuilder = new StatementBuilder((IDecompilerTypeSystem)typeSystem, decompilationContext, function, localSettings, CancellationToken);
                    body = statementBuilder.ConvertAsBlock(function.Body);

                    Comment prev = null;
                    foreach (string warning in function.Warnings)
                    {
                        body.InsertChildAfter(prev, prev = new Comment(warning), Roles.Comment);
                    }

                    entityDecl.AddChild(body, Roles.Body);
                }
                entityDecl.AddAnnotation(function);

                if (function.IsIterator)
                {
                    if (localSettings.DecompileMemberBodies && !body.Descendants.Any(d => d is YieldReturnStatement || d is YieldBreakStatement))
                    {
                        body.Add(new YieldBreakStatement());
                    }
                    RemoveAttribute(entityDecl, KnownAttribute.IteratorStateMachine);
                    if (function.StateMachineCompiledWithMono)
                    {
                        RemoveAttribute(entityDecl, KnownAttribute.DebuggerHidden);
                    }
                }
                if (function.IsAsync)
                {
                    entityDecl.Modifiers |= Modifiers.Async;
                    RemoveAttribute(entityDecl, KnownAttribute.AsyncStateMachine);
                    RemoveAttribute(entityDecl, KnownAttribute.DebuggerStepThrough);
                }
            }
            catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException))
            {
                throw new DecompilerException(method, innerException);
            }
        }

        bool RemoveAttribute(EntityDeclaration entityDecl, KnownAttribute attributeType)
        {
            bool found = false;
            foreach (var section in entityDecl.Attributes)
            {
                foreach (var attr in section.Attributes)
                {
                    var symbol = attr.Type.GetSymbol();
                    if (symbol is ITypeDefinition td && td.FullTypeName == attributeType.GetTypeName())
                    {
                        attr.Remove();
                        found = true;
                    }
                }
                if (section.Attributes.Count == 0)
                {
                    section.Remove();
                }
            }
            return found;
        }

        bool FindAttribute(EntityDeclaration entityDecl, KnownAttribute attributeType, out ICSharpCode.Decompiler.CSharp.Syntax.Attribute attribute)
        {
            attribute = null;
            foreach (var section in entityDecl.Attributes)
            {
                foreach (var attr in section.Attributes)
                {
                    var symbol = attr.Type.GetSymbol();
                    if (symbol is ITypeDefinition td && td.FullTypeName == attributeType.GetTypeName())
                    {
                        attribute = attr;
                        return true;
                    }
                }
            }
            return false;
        }

        void AddDefinesForConditionalAttributes(ILFunction function, DecompileRun decompileRun)
        {
            foreach (var call in function.Descendants.OfType<CallInstruction>())
            {
                var attr = call.Method.GetAttribute(KnownAttribute.Conditional, inherit: true);
                var symbolName = attr?.FixedArguments.FirstOrDefault().Value as string;
                if (symbolName == null || !decompileRun.DefinedSymbols.Add(symbolName))
                    continue;
                syntaxTree.InsertChildAfter(null, new PreProcessorDirective(PreProcessorDirectiveType.Define, symbolName), Roles.PreProcessorDirective);
            }
        }

        EntityDeclaration DoDecompile(IField field, DecompileRun decompileRun, ITypeResolveContext decompilationContext)
        {
            Debug.Assert(decompilationContext.CurrentMember == field);
            try
            {
                var typeSystemAstBuilder = CreateAstBuilder(decompilationContext);
                if (decompilationContext.CurrentTypeDefinition.Kind == TypeKind.Enum && field.IsConst)
                {
                    var enumDec = new EnumMemberDeclaration { Name = field.Name };
                    object constantValue = field.GetConstantValue();
                    if (constantValue != null) {
                        long initValue = (long)CSharpPrimitiveCast.Cast(TypeCode.Int64, constantValue, false);
                        enumDec.Initializer = typeSystemAstBuilder.ConvertConstantValue(decompilationContext.CurrentTypeDefinition.EnumUnderlyingType, constantValue);
                        if (enumDec.Initializer is PrimitiveExpression primitive
                            && initValue >= 0 && (decompilationContext.CurrentTypeDefinition.HasAttribute(KnownAttribute.Flags)
                                || (initValue > 9 && (unchecked(initValue & (initValue - 1)) == 0 || unchecked(initValue & (initValue + 1)) == 0))))
                        {
                            primitive.SetValue(initValue, $"0x{initValue:X}");
                        }
                        enumDec.Attributes.AddRange(field.GetAttributes().Select(a => new AttributeSection(typeSystemAstBuilder.ConvertAttribute(a))));
                        enumDec.AddAnnotation(new MemberResolveResult(null, field));
                        return enumDec;
                    }
                }
                bool isMathPIOrE = ((field.Name == "PI" || field.Name == "E") && (field.DeclaringType.FullName == "System.Math" || field.DeclaringType.FullName == "System.MathF"));
                typeSystemAstBuilder.UseSpecialConstants = !(field.DeclaringType.Equals(field.ReturnType) || isMathPIOrE);
                var fieldDecl = typeSystemAstBuilder.ConvertEntity(field);
                SetNewModifier(fieldDecl);
                if (settings.FixedBuffers && IsFixedField(field, out var elementType, out var elementCount))
                {
                    var fixedFieldDecl = new FixedFieldDeclaration();
                    fieldDecl.Attributes.MoveTo(fixedFieldDecl.Attributes);
                    fixedFieldDecl.Modifiers = fieldDecl.Modifiers;
                    fixedFieldDecl.ReturnType = typeSystemAstBuilder.ConvertType(elementType);
                    fixedFieldDecl.Variables.Add(new FixedVariableInitializer(field.Name, new PrimitiveExpression(elementCount)));
                    fixedFieldDecl.Variables.Single().CopyAnnotationsFrom(((FieldDeclaration)fieldDecl).Variables.Single());
                    fixedFieldDecl.CopyAnnotationsFrom(fieldDecl);
                    RemoveAttribute(fixedFieldDecl, KnownAttribute.FixedBuffer);
                    return fixedFieldDecl;
                }
                // var fieldDefinition = metadata.GetFieldDefinition((FieldDefinitionHandle)field.MetadataToken);
                // if (fieldDefinition.HasFlag(System.Reflection.FieldAttributes.HasFieldRVA))
                // {
                //     // Field data as specified in II.16.3.2 of ECMA-335 6th edition:
                //     // .data I_X = int32(123)
                //     // .field public static int32 _x at I_X
                //     string message;
                //     try
                //     {
                //         var initVal = fieldDefinition.GetInitialValue(module.PEFile.Reader, TypeSystem);
                //         message = string.Format(" Not supported: data({0}) ", BitConverter.ToString(initVal.ReadBytes(initVal.RemainingBytes)).Replace('-', ' '));
                //     }
                //     catch (BadImageFormatException ex)
                //     {
                //         message = ex.Message;
                //     }
                //     ((FieldDeclaration)fieldDecl).Variables.Single().AddChild(new Comment(message, CommentType.MultiLine), Roles.Comment);
                // }
                return fieldDecl;
            }
            catch (Exception innerException) when (!(innerException is OperationCanceledException || innerException is DecompilerException)) {
                throw new DecompilerException(module, field, innerException);
            }

        }

        internal static bool IsFixedField(IField field, out IType type, out int elementCount)
        {
            type = null;
            elementCount = 0;
            IAttribute attr = field.GetAttribute(KnownAttribute.FixedBuffer, inherit: false);
            if (attr != null && attr.FixedArguments.Length == 2)
            {
                if (attr.FixedArguments[0].Value is IType trr && attr.FixedArguments[1].Value is int length)
                {
                    type = trr;
                    elementCount = length;
                    return true;
                }
            }
            return false;
        }

        EntityDeclaration DoDecompile(IProperty property, DecompileRun decompileRun, ITypeResolveContext decompilationContext)
        {
            Debug.Assert(decompilationContext.CurrentMember == property);
            var typeSystemAstBuilder = CreateAstBuilder(decompilationContext);
            EntityDeclaration propertyDecl = typeSystemAstBuilder.ConvertEntity(property);
            if (property.IsExplicitInterfaceImplementation && !property.IsIndexer)
            {
                int lastDot = property.Name.LastIndexOf('.');
                propertyDecl.Name = property.Name.Substring(lastDot + 1);
            }
            FixParameterNames(propertyDecl);
            Accessor getter, setter;
            if (propertyDecl is PropertyDeclaration)
            {
                getter = ((PropertyDeclaration)propertyDecl).Getter;
                setter = ((PropertyDeclaration)propertyDecl).Setter;
            }
            else
            {
                getter = ((IndexerDeclaration)propertyDecl).Getter;
                setter = ((IndexerDeclaration)propertyDecl).Setter;
            }
            if (property.CanGet && property.Getter.HasBody)
            {
                DecompileBody(property.Getter, getter, decompileRun, decompilationContext);
            }
            if (property.CanSet && property.Setter.HasBody)
            {
                DecompileBody(property.Setter, setter, decompileRun, decompilationContext);
            }
            var accessor = property.Getter as IMethodWithDefinition ?? property as IMethodWithDefinition;
            if (accessor != null && accessor.HasFlag(System.Reflection.MethodAttributes.Virtual) == accessor.HasFlag(System.Reflection.MethodAttributes.NewSlot))
                SetNewModifier(propertyDecl);
            return propertyDecl;
        }

        EntityDeclaration DoDecompile(IEvent ev, DecompileRun decompileRun, ITypeResolveContext decompilationContext)
        {
            Debug.Assert(decompilationContext.CurrentMember == ev);
            var typeSystemAstBuilder = CreateAstBuilder(decompilationContext);
            typeSystemAstBuilder.UseCustomEvents = ev.DeclaringTypeDefinition.Kind != TypeKind.Interface;
            var eventDecl = typeSystemAstBuilder.ConvertEntity(ev);
            int lastDot = ev.Name.LastIndexOf('.');
            if (ev.IsExplicitInterfaceImplementation)
            {
                eventDecl.Name = ev.Name.Substring(lastDot + 1);
            }
            if (ev.CanAdd && ev.AddAccessor.HasBody)
            {
                DecompileBody(ev.AddAccessor, ((CustomEventDeclaration)eventDecl).AddAccessor, decompileRun, decompilationContext);
            }
            if (ev.CanRemove && ev.RemoveAccessor.HasBody)
            {
                DecompileBody(ev.RemoveAccessor, ((CustomEventDeclaration)eventDecl).RemoveAccessor, decompileRun, decompilationContext);
            }
            var accessor = ev.AddAccessor as IMethodWithDefinition ?? ev.RemoveAccessor as IMethodWithDefinition;
            if (accessor != null && accessor.HasFlag(System.Reflection.MethodAttributes.Virtual) == accessor.HasFlag(System.Reflection.MethodAttributes.NewSlot))
            {
                SetNewModifier(eventDecl);
            }
            return eventDecl;
        }
    }
}
