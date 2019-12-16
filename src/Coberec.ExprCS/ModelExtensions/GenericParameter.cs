using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coberec.ExprCS
{
    public partial class GenericParameter
    {
        static readonly ConcurrentDictionary<(string ownerName, string name), GenericParameter> typeParameterAssignment = new ConcurrentDictionary<(string, string), GenericParameter>();
        public static GenericParameter Get(string ownerName, string name) =>
            typeParameterAssignment.GetOrAdd((ownerName, name), x => new GenericParameter(Guid.NewGuid(), x.name));

        public static GenericParameter FromType(Type t)
        {
            Assert.True(t.IsGenericParameter);
            return Get(((object)t.DeclaringMethod ?? t.DeclaringType).ToString(), t.Name);
        }
    }
}
