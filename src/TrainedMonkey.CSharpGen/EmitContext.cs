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

namespace Coberec.CSharpGen
{
    public sealed class EmitSettings
    {
        public EmitSettings(string @namespace, ImmutableDictionary<string, FullTypeName> primitiveTypeMapping, ImmutableDictionary<string, ValidatorConfig> validators = null)
        {
            Namespace = @namespace;
            PrimitiveTypeMapping = primitiveTypeMapping;
            Validators = validators ?? ImmutableDictionary<string, ValidatorConfig>.Empty;
        }

        public bool EmitInterfaceWithMethods { get; } = true;
        public bool EmitOptionalWithMethods { get; } = true;
        public bool EmitWithMethods { get; } = true;

        public string Namespace { get; }
        public ImmutableDictionary<string, FullTypeName> PrimitiveTypeMapping { get; }
        public ImmutableDictionary<string, ValidatorConfig> Validators { get; }
    }
    public sealed class EmitContext
    {
        public EmitContext(HackedSimpleCompilation hackedSimpleCompilation, EmitSettings settings, DataSchema fullSchema)
        {
            HackedSimpleCompilation = hackedSimpleCompilation;
            Module = (VirtualModule)Compilation.MainModule;
            Settings = settings;
            FullSchema = fullSchema;
        }

        public HackedSimpleCompilation HackedSimpleCompilation { get; }

        public VirtualModule Module { get; }

        public ICompilation Compilation => HackedSimpleCompilation;

        public EmitSettings Settings { get; }
        public DataSchema FullSchema { get; }

        public IType FindType(Type t) => Compilation.FindType(t);
        public IType FindType<T>() => Compilation.FindType(typeof(T));
        public IType FindType(FullTypeName name) => new GetClassTypeReference(name).Resolve(new SimpleTypeResolveContext(Compilation));
        public IMethod FindMethod<TResult>(Expression<Func<TResult>> expr)
        {
            var body = expr.Body;
            var methodInfo = body is MethodCallExpression call ? call.Method :
                                body is NewExpression @new ? (MethodBase)@new.Constructor :
                                throw new NotSupportedException($"Expression '{expr}' of type '{body}' is not supported");

            var t = FindType(methodInfo.DeclaringType);
            var parameters = methodInfo.GetParameters();
            // TODO: also check arg types
            var method = t.GetDefinition().Methods.Where(m => m.Name == methodInfo.Name && m.Parameters.Count == parameters.Length).Single();

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