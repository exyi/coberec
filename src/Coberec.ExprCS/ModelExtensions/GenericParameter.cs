using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Represents a generic parameter of a type or method. </summary>
    public partial class GenericParameter
    {
        static readonly ConcurrentDictionary<(string ownerName, string name), GenericParameter> typeParameterAssignment = new ConcurrentDictionary<(string, string), GenericParameter>();

        /// <summary> Gets a generic parameter creates for the specified member. The method creates a new parameter (with unique ID) only once, stores the answer and reuses it. </summary>
        public static GenericParameter Get(string ownerName, string name) =>
            typeParameterAssignment.GetOrAdd((ownerName, name), x => new GenericParameter(Guid.NewGuid(), x.name));

        /// <summary> Converts <see cref="Type" /> from standard reflection. The Type must have `IsGenericParameter == true` </summary>
        public static GenericParameter FromType(Type t)
        {
            Assert.True(t.IsGenericParameter);
            return Get(((object)t.DeclaringMethod ?? t.DeclaringType).ToString(), t.Name);
        }
    }
}
