using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Coberec.Utils;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Represents a reference to a type. May be a type (with filled in generic arguments), array, pointer, ... </summary>
    public partial class TypeReference
    {
        /// <summary> Says if the type is object-like. Says `true` for classes, arrays, functions. Says `false` for value types, pointers. And says `null` for stuff that is unknown - generic parameters and by-ref types. </summary>
        public bool? IsReferenceType => this.Match<bool?>(
            specializedType: t => !t.Type.IsValueType,
            arrayType: _ => true,
            byReferenceType: _ => null,
            pointerType: _ => false,
            genericParameter: _ => null,
            functionType: _ => true
        );

        /// <summary> Returns true, if the type can not have any subclasses </summary>
        public bool IsSealed() => this.Match<bool>(
            specializedType: t => t.Type.IsValueType || (!t.Type.CanOverride && t.Type.Kind != "delegate"),
            //                                                                             ^ TODO: more involved (co/contra) variance check?
            arrayType: t => t.Type.IsSealed(),
            byReferenceType: _ => true,
            pointerType: _ => false,
            genericParameter: _ => false,
            functionType: _ => false
        );

        public static implicit operator TypeReference(TypeSignature signature)
        {
            if (signature == null) return null;
            if (signature.TypeParameters.Any())
                throw new Exception($"Implicit conversion TypeSignature -> TypeReference expects that the type does not have generic parameters. However, {signature} has parameters [{string.Join(", ", signature.TypeParameters.Select(p => p.Name))}]");
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

        public TypeReference SubstituteGenerics(
            IEnumerable<GenericParameter> parameters,
            IEnumerable<TypeReference> arguments) =>
            SubstituteGenerics(parameters.ToImmutableArray(), arguments.ToImmutableArray());
        public TypeReference SubstituteGenerics(
            ImmutableArray<GenericParameter> parameters,
            ImmutableArray<TypeReference> arguments)
        {
            Assert.Equal(parameters.Length, arguments.Length);
            if (parameters.Length == 0)
                return this;
            Assert.Equal(parameters.Length, parameters.Distinct().Count());
            return this.Match(
                specializedType: t => t.SubstituteGenerics(parameters, arguments),
                arrayType: t => t.With(type: t.Type.SubstituteGenerics(parameters, arguments)),
                byReferenceType: t => t.With(type: t.Type.SubstituteGenerics(parameters, arguments)),
                pointerType: t => t.With(type: t.Type.SubstituteGenerics(parameters, arguments)),
                genericParameter:
                    t => parameters.IndexOf(t) switch {
                        -1    => t,
                        var i => arguments[i]
                    },
                functionType:
                    t => t.With(
                        t.Params.EagerSelect(t => t.SubstituteGenerics(parameters, arguments)),
                        t.ResultType.SubstituteGenerics(parameters, arguments))
            );
        }

        public T MatchST<T>(Func<SpecializedType, T> specializedType, Func<TypeReference, T> otherwise) =>
            this is SpecializedTypeCase s ? specializedType(s.Item) :
            otherwise(this);

        /// <summary> Creates a specialization of System.ValueTuple for the specified <paramref name="types" />. </summary>
        public static TypeReference Tuple(IEnumerable<TypeReference> types) => Tuple(types.ToImmutableArray());
        /// <summary> Creates a specialization of System.ValueTuple for the specified <paramref name="types" />. </summary>
        public static TypeReference Tuple(params TypeReference[] types) => Tuple(types.ToImmutableArray());

        /// <summary> Creates a specialization of System.ValueTuple for the specified <paramref name="types" />. </summary>
        public static TypeReference Tuple(ImmutableArray<TypeReference> types)
        {
            return types.Length switch {
                0 => TypeSignature.ValueTuple0,
                1 => TypeSignature.ValueTuple1.Specialize(types),
                2 => TypeSignature.ValueTuple2.Specialize(types),
                3 => TypeSignature.ValueTuple3.Specialize(types),
                4 => TypeSignature.ValueTuple4.Specialize(types),
                5 => TypeSignature.ValueTuple5.Specialize(types),
                6 => TypeSignature.ValueTuple6.Specialize(types),
                7 => TypeSignature.ValueTuple7.Specialize(types),
                _ => TypeSignature.ValueTupleRest.Specialize(types.EagerSlice(take: 7).Add(Tuple(types.EagerSlice(skip: 7))))
            };
        }

        public static TypeReference FromType(System.Type type)
        {
            if (type.IsArray)
                return TypeReference.ArrayType(FromType(type.GetElementType()), type.GetArrayRank());
            else if (type.IsPointer)
                return TypeReference.PointerType(FromType(type.GetElementType()));
            else if (type.IsByRef)
                return TypeReference.ByReferenceType(FromType(type.GetElementType()));
            else if (type.IsGenericParameter)
                return Coberec.ExprCS.GenericParameter.FromType(type);
            else if (type.IsGenericType)
            {
                // This check is probably invalid since in Reflection you encounter these type definitions even in return types...
                // if (type.IsGenericTypeDefinition)
                //     throw new ArgumentException($"Can not create TypeReference from open (unconstructed) generic type {type}", nameof(type));
                var args = type.GetGenericArguments().EagerSelect(FromType);
                var signature = TypeSignature.FromType(type.GetGenericTypeDefinition());
                return TypeReference.SpecializedType(signature, args);
            }
            else
                return TypeSignature.FromType(type);
        }
    }
}
