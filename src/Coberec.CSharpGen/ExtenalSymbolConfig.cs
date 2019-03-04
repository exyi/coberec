using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CoreLib;

namespace Coberec.CSharpGen
{
    public sealed class ExternalSymbolConfig
    {
        public ExternalSymbolConfig(
            string declaredIn,
            string name,
            ExternalSymbolKind kind,
            string resultType = null,
            ImmutableArray<SymbolArgumentConfig> args = default)
        {
            DeclaredIn = declaredIn;
            Name = name;
            Kind = kind;
            Args = args.IsDefault ? ImmutableArray<SymbolArgumentConfig>.Empty : args;
            ResultType = resultType;
        }

        public string DeclaredIn { get; }
        public string Name { get; }
        public ExternalSymbolKind Kind { get; }
        public ImmutableArray<SymbolArgumentConfig> Args { get; }
        public string ResultType { get; }
    }

    public sealed class SymbolArgumentConfig
    {
        public SymbolArgumentConfig(string name, string type)
        {
            Type = type;
            Name = name;
        }

        public string Type { get; }
        public string Name { get; }
    }

    public enum ExternalSymbolKind {
        TypeDefinition,
        Method,
        StaticMethod
    }
}
