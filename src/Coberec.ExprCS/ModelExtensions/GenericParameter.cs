using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TS = ICSharpCode.Decompiler.TypeSystem;
using R = System.Reflection;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Represents a generic parameter of a type or method. </summary>
    public partial class GenericParameter
    {
        static readonly ConcurrentDictionary<(string ownerName, string name), GenericParameter> typeParameterAssignment = new ConcurrentDictionary<(string, string), GenericParameter>();

        static string OwnerToString(object owner) => owner switch {
            Type type => SymbolFormatter.TypeDefToString(type),
            TS.IType type => SymbolFormatter.TypeDefToString(type),
            R.MethodInfo method => SymbolFormatter.MethodToString(method),
            TS.IMethod method => SymbolFormatter.MethodToString(method),
            _ => throw new ArgumentException($"Can not handle owner '{owner}' of type {owner?.GetType().FullName ?? "null"}.", nameof(owner))
        };

        /// <summary> Gets a generic parameter creates for the specified member. The method creates a new parameter (with unique ID) only once, stores the answer and reuses it. </summary>
        public static GenericParameter Get(object owner, string name) =>
            typeParameterAssignment.GetOrAdd((OwnerToString(owner), name), x => new GenericParameter(Guid.NewGuid(), x.name));

        /// <summary> Converts <see cref="Type" /> from standard reflection. The Type must have `IsGenericParameter == true` </summary>
        public static GenericParameter FromType(Type t)
        {
            Assert.True(t.IsGenericParameter);
            return Get(((object)t.DeclaringMethod ?? t.DeclaringType), t.Name);
        }

        public override string ToString() => $"{this.Name}({this.Id})";
    }
}
