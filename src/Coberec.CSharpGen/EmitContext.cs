using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using ICSharpCode.Decompiler.IL;
using IL = ICSharpCode.Decompiler.IL;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using Coberec.CSharpGen.TypeSystem;
using System.Diagnostics;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler;
using Coberec.CSharpGen.Emit;
using System.Linq.Expressions;
using System.Reflection;
using System.Globalization;
using System.Text;
using Coberec.MetaSchema;
using System.Collections.Immutable;
using ICSharpCode.Decompiler.CSharp.OutputVisitor;
using Coberec.CoreLib;
using E=Coberec.ExprCS;

namespace Coberec.CSharpGen
{
    public sealed class EmitSettings
    {
        public EmitSettings(
            string @namespace,
            ImmutableDictionary<string, string> primitiveTypeMapping = null,
            ImmutableDictionary<string, ValidatorConfig> validators = null,
            IEnumerable<ExternalSymbolConfig> externalSymbols = null,
            IEnumerable<string> additionalReferences = null,
            bool emitWithMethods = true,
            bool emitInterfaceWithMethods = true,
            bool emitOptionalWithMethods = true,
            bool withMethodReturnsValidationResult = true,
            bool fallbackToStringType = false,
            bool addJsonPropertyAttributes = false,
            bool emitPartialClasses = false,
            bool emitValidationExtension = false)
        {
            Namespace = @namespace;
            PrimitiveTypeMapping = primitiveTypeMapping ?? ImmutableDictionary<string, string>.Empty;
            Validators = validators ?? ImmutableDictionary<string, ValidatorConfig>.Empty;
            ExternalSymbols = externalSymbols?.ToImmutableArray() ?? ImmutableArray<ExternalSymbolConfig>.Empty;
            AdditionalReferences = additionalReferences?.ToImmutableArray() ?? ImmutableArray<string>.Empty;
            EmitWithMethods = emitWithMethods;
            EmitInterfaceWithMethods = emitInterfaceWithMethods;
            EmitOptionalWithMethods = emitOptionalWithMethods;
            WithMethodReturnValidationResult = withMethodReturnsValidationResult;
            FallbackToStringType = fallbackToStringType;
            AddJsonPropertyAttributes = addJsonPropertyAttributes;
            EmitPartialClasses = emitPartialClasses;
            if (emitValidationExtension && !emitPartialClasses)
                throw new ArgumentException("Validation extension can only be enabled with partial classes", nameof(emitValidationExtension));
            EmitValidationExtension = emitValidationExtension;
        }

        public bool EmitInterfaceWithMethods { get; } = true;
        public bool EmitOptionalWithMethods { get; } = true;
        public bool EmitWithMethods { get; } = true;
        public bool WithMethodReturnValidationResult { get; } = true;
        public bool FallbackToStringType { get; } = false;
        public string Namespace { get; }
        public ImmutableDictionary<string, string> PrimitiveTypeMapping { get; }
        public ImmutableDictionary<string, ValidatorConfig> Validators { get; }
        public ImmutableArray<ExternalSymbolConfig> ExternalSymbols { get; }
        public ImmutableArray<string> AdditionalReferences { get; }
        public bool AddJsonPropertyAttributes { get; }
        public bool EmitPartialClasses { get; }
        public bool EmitValidationExtension { get; }

        public EmitSettings With(
            OptParam<string> @namespace = default,
            OptParam<ImmutableDictionary<string, string>> primitiveTypeMapping = default,
            OptParam<ImmutableDictionary<string, ValidatorConfig>> validators = default,
            OptParam<IEnumerable<ExternalSymbolConfig>> externalSymbols = default,
            OptParam<IEnumerable<string>> additionalReferences = default,
            OptParam<bool> emitOptionalWithMethod = default,
            OptParam<bool> fallbackToStringType = default,
            OptParam<bool> addJsonPropertyAttributes = default,
            OptParam<bool> emitPartialClasses = default,
            OptParam<bool> emitValidationExtension = default
        )
        {
            return new EmitSettings(
                @namespace.ValueOrDefault(this.Namespace),
                primitiveTypeMapping.ValueOrDefault(this.PrimitiveTypeMapping),
                validators.ValueOrDefault(this.Validators),
                externalSymbols.ValueOrDefault(this.ExternalSymbols),
                additionalReferences.ValueOrDefault(this.AdditionalReferences),
                this.EmitWithMethods,
                this.EmitInterfaceWithMethods,
                emitOptionalWithMethod.ValueOrDefault(this.EmitOptionalWithMethods),
                this.WithMethodReturnValidationResult,
                fallbackToStringType.ValueOrDefault(this.FallbackToStringType),
                addJsonPropertyAttributes.ValueOrDefault(this.AddJsonPropertyAttributes),
                emitPartialClasses.ValueOrDefault(this.EmitPartialClasses),
                emitValidationExtension.ValueOrDefault(this.EmitValidationExtension)
            );
        }
    }
    public sealed class EmitContext
    {
        public EmitContext(E.MetadataContext metadata, EmitSettings settings, DataSchema fullSchema)
        {
            var compilation = metadata.Compilation;

            // TODO: move this assert to ExprCS
            foreach (var t in Enum.GetValues(typeof(KnownTypeCode)))
            {
                var ft = compilation.FindType((KnownTypeCode)t);
                Debug.Assert(!(ft is UnknownType) || KnownTypeCode.Unsafe.Equals(t));
            }

            Metadata = metadata;
            Compilation = compilation;
            Module = Compilation.MainModule;
            Settings = settings;
            FullSchema = fullSchema;
        }

        public IModule Module { get; }
        public E.MetadataContext Metadata { get; }
        public ICompilation Compilation { get; }

        public EmitSettings Settings { get; }
        public DataSchema FullSchema { get; }

        public IType FindType(Type t) => Compilation.FindType(t);
        public IType FindType<T>() => Compilation.FindType(typeof(T));
        public IType FindType(FullTypeName name) => new GetClassTypeReference(name).Resolve(new SimpleTypeResolveContext(Compilation));
        public E.MethodSignature FindMethod(string method)
        {
            // TODO: move to ExprCS
            var type = Metadata.FindTypeDef(method.Substring(0, method.LastIndexOf('.')));
            return Metadata.GetMemberMethodDefs(type, method.Substring(method.LastIndexOf('.') + 1)).SingleOrDefault() ??
                   E.SymbolLoader.Method(Compilation.GetAllTypeDefinitions().SelectMany(t => t.GetMethods(m => m.FullName == method)).Single()); // TODO: it this useful for anything
        }
        public IMethod FindMethod(Expression<Action> expr)
        {
            var body = expr.Body;
            var methodInfo = body is MethodCallExpression call ? call.Method :
                             body is NewExpression @new ? (MethodBase)@new.Constructor :
                             throw new NotSupportedException($"Expression '{expr}' of type '{body}' is not supported");

            var t = FindType(methodInfo.DeclaringType);
            var parameters = methodInfo.GetParameters();
            // TODO: also check arg types
            var method = t.GetDefinition().Methods.Where(m => m.Name == methodInfo.Name && m.Parameters.Select(p => p.Type.ReflectionName).SequenceEqual(parameters.Select(p => p.ParameterType.FullName))).SingleOrDefault() ??
                throw new Exception($"Could not find method {methodInfo}.");

            var methodGenericArgs = methodInfo.IsGenericMethod ?
                                    methodInfo.GetGenericArguments().Select(FindType).ToArray() :
                                    null;
            var typeGenericArgs = methodInfo.DeclaringType.IsGenericType ?
                                  methodInfo.DeclaringType.GetGenericArguments().Select(FindType).ToArray() :
                                  null;


            if (typeGenericArgs != null || methodGenericArgs != null)
                method = method.Specialize(new TypeParameterSubstitution(typeGenericArgs, methodGenericArgs));

            return method;
        }
    }
}
