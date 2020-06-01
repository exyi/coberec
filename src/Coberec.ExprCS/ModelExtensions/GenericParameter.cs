using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TS = ICSharpCode.Decompiler.TypeSystem;
using R = System.Reflection;
using Xunit;

namespace Coberec.ExprCS
{
    public partial class GenericParameter
    {
        static readonly ConcurrentDictionary<(string ownerName, string name), GenericParameter> typeParameterAssignment = new ConcurrentDictionary<(string, string), GenericParameter>();

        /// <summary> Creates a new unique generic parameter. </summary>
        public static GenericParameter Create(string name) => new GenericParameter(Guid.NewGuid(), name);

        static string OwnerToString(object owner) => owner switch {
            Type type => SymbolFormatter.TypeDefToString(type),
            TS.IType type => SymbolFormatter.TypeDefToString(type),
            R.MethodInfo method => SymbolFormatter.MethodToString(method),
            TS.IMethod method => SymbolFormatter.MethodToString(method),
            _ => throw new ArgumentException($"Can not handle owner '{owner}' of type {owner?.GetType().FullName ?? "null"}.", nameof(owner))
        };

        /// <summary> Gets a generic parameter creates for the specified member. The method creates a new parameter (with unique ID) only once, stores the answer and reuses it. </summary>
        internal static GenericParameter Get(object owner, string name) =>
            typeParameterAssignment.GetOrAdd((OwnerToString(owner), name), x => new GenericParameter(Guid.NewGuid(), x.name));

        /// <summary> Converts <see cref="Type" /> from standard reflection. The Type must have `IsGenericParameter == true` </summary>
        public static GenericParameter FromType(Type t)
        {
            Assert.True(t.IsGenericParameter);
            if (t.DeclaringMethod is object)
                return Get(t.DeclaringMethod, t.Name);
            // hack: Reflection API returns that owner of T from ArraySegment<T>.Enumerator is the Enumerator instead of the ArraySegment :/
            // this piece of ... code corrects that by finding the matching generic type on it's parent

            IEnumerable<Type> allGenericParams(Type t) =>
                t.DeclaringType is null ? t.GetGenericArguments()
                                        : allGenericParams(t.DeclaringType)
                                          .Concat(t.GetGenericArguments().Skip(t.DeclaringType.GetGenericArguments().Length));
            var tparams = allGenericParams(t.DeclaringType).ToArray();
            var t2 = tparams[t.GenericParameterPosition];
            Assert.Equal(t2.Name, t.Name);
            Assert.Equal(t2.GenericParameterPosition, t.GenericParameterPosition);
            return Get(t2.DeclaringType, t2.Name);
        }
    }
}
