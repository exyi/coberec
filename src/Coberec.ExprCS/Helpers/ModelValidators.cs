using System;
using Coberec.CoreLib;

namespace Coberec.ExprCS
{
    public static class ModelValidators
    {
        public static ValidationErrors TypeIsNotRef(TypeReference type)
        {
            if (type is TypeReference.ByReferenceTypeCase byRef)
                return ValidationErrors.Create($"By reference type {type} can not be used when it would be put on a heap.");
            // TODO: ref structs
            return null;
        }
    }
}
