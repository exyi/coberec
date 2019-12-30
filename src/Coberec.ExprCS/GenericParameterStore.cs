using System;
using System.Collections.Concurrent;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;

namespace Coberec.ExprCS
{
    internal class GenericParameterStore
    {
        private ConcurrentDictionary<Guid, ITypeParameter> store = new ConcurrentDictionary<Guid, ITypeParameter>();

        public ITypeParameter Register(GenericParameter parameter, IEntity owner, int index)
        {
            if (store.TryGetValue(parameter.Id, out var existingP))
            {
                if (!existingP.Owner.Equals(owner))
                    throw new Exception($"There is collision in the usage of generic parameter {parameter}: both {existingP.Owner} and {owner} use it.");
                if (existingP.Index != index)
                    throw new Exception($"There is collision in the usage of generic parameter {parameter} on {existingP.Owner}. It is used at index {existingP.Index} and {index}.");
                return existingP;
            }

            var newP = new DefaultTypeParameter(owner, index, name: NameSanitizer.SanitizeCsharpName(parameter.Name, lowerCase: null));
            SymbolLoader.RegisterTypeParameter(newP, parameter);
            store.TryAdd(parameter.Id, newP);
            return Register(parameter, owner, index);
        }

        public ITypeParameter Retreive(GenericParameter parameter)
        {
            if (store.TryGetValue(parameter.Id, out var p))
                return p;
            else
                throw new Exception($"Type parameter {parameter} is not known at this time");
        }
    }
}
