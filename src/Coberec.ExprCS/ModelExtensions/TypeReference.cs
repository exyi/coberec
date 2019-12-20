using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.CSharpGen;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Represents a reference to a type. May be a type (with filled in generic arguments), array, pointer, ... </summary>
    public partial class TypeReference
    {
        /// <summary> Says if the type is object-like. Says `true` for classes, arrays, functions. Says `false` for value types, pointers. And says `null` for stuff that is unknown - generic parameters and by-ref types. </summary>
        public bool? IsReferenceType => this.Match<bool?>(
            specializedType: t => !t.Item.Type.IsValueType,
            arrayType: _ => true,
            byReferenceType: _ => null,
            pointerType: _ => false,
            genericParameter: _ => null,
            functionType: _ => true
        );

        public static implicit operator TypeReference(TypeSignature signature)
        {
            Assert.Empty(signature.TypeParameters);
            return new SpecializedType(signature, ImmutableArray<TypeReference>.Empty);
        }

        /// <summary> If the type is a <see cref="ByReferenceTypeCase" /> it is unwrapped. Otherwise it is unchanged. </summary>
        public TypeReference UnwrapReference() =>
            this is ByReferenceTypeCase byref ? byref.Item.Type
                                              : this;

        /// <summary> Returns true if this type reference is a specialization of the generic type definition in <paramref name="type" />. </summary>
        public bool IsGenericInstanceOf(TypeSignature type) =>
            this is SpecializedTypeCase stc && stc.Item.Type == type;

        /// <summary> Returns true if this type reference is a specialization of the generic type definition in <paramref name="type" />. </summary>
        public bool IsGenericInstanceOf(System.Type type) =>
            this is SpecializedTypeCase stc && stc.Item.Type == TypeSignature.FromType(type);

        // TODO: this should be generated
        public override string ToString() => this.Match(
            x => x.Item.ToString(),
            x => x.Item.ToString(),
            x => x.Item.ToString(),
            x => x.Item.ToString(),
            x => x.Item.ToString(),
            x => x.Item.ToString()
        );

        public TypeReference SubstituteGenerics(
            IEnumerable<GenericParameter> parameters,
            IEnumerable<TypeReference> arguments) =>
            SubstituteGenerics(parameters.ToImmutableArray(), arguments.ToImmutableArray());
        public TypeReference SubstituteGenerics(
            ImmutableArray<GenericParameter> parameters,
            ImmutableArray<TypeReference> arguments)
        {
            Assert.Equal(parameters.Length, arguments.Length);
            Assert.Equal(parameters.Length, parameters.Distinct().Count());
            return this.Match(
                specializedType: t => t.Item.With(genericParameters: t.Item.GenericParameters.EagerSelect(t => t.SubstituteGenerics(parameters, arguments))),
                arrayType: t => t.Item.With(type: t.Item.Type.SubstituteGenerics(parameters, arguments)),
                byReferenceType: t => t.Item.With(type: t.Item.Type.SubstituteGenerics(parameters, arguments)),
                pointerType: t => t.Item.With(type: t.Item.Type.SubstituteGenerics(parameters, arguments)),
                genericParameter:
                    t => parameters.IndexOf(t.Item) switch {
                        -1    => t,
                        var i => arguments[i]
                    },
                functionType:
                    t => t.Item.With(
                        t.Item.Params.EagerSelect(t => t.SubstituteGenerics(parameters, arguments)),
                        t.Item.ResultType.SubstituteGenerics(parameters, arguments))
            );
        }

        public static TypeReference FromType(System.Type type)
        {
            if (type.IsArray)
                return TypeReference.ArrayType(FromType(type.GetElementType()), type.GetArrayRank());
            else if (type.IsPointer)
                return TypeReference.PointerType(FromType(type.GetElementType()));
            else if (type.IsByRef)
                return TypeReference.ByReferenceType(FromType(type.GetElementType()));
            else if (type.IsGenericType)
            {
                Assert.True(type.IsConstructedGenericType);
                var args = type.GenericTypeArguments.EagerSelect(FromType);
                var signature = TypeSignature.FromType(type.GetGenericTypeDefinition());
                return TypeReference.SpecializedType(signature, args);
            }
            else
                return TypeSignature.FromType(type);
        }
    }
}
