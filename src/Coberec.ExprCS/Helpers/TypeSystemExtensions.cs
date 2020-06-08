// Inspired by TypeSystemExtension from ILSpy project

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary>
    /// Contains extension methods for the type system.
    /// </summary>
    public static class TypeSystemExtensions
    {
        /// <summary>
        /// Gets whether this type definition is derived from the base type definition.
        /// </summary>
        public static bool IsDerivedFrom(this SpecializedType type, SpecializedType baseType, MetadataContext cx)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            if (baseType == null)
                return false;
            return cx.GetBaseTypes(type).Contains(baseType);
        }

        /// <summary>
        /// Returns all declaring types of this type.
        /// The output is ordered so that inner types occur before outer types.
        /// </summary>
        public static IEnumerable<SpecializedType> GetDeclaringTypes(this SpecializedType type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            while (type is object) {
                yield return type;
                type = type.DeclaringType();
            }
        }

        /// <summary>
        /// Gets whether the type is an open type (contains type parameters).
        /// </summary>
        /// <example>
        /// <code>
        /// class X&lt;T&gt; {
        ///   List&lt;T&gt; open;
        ///   X&lt;X&lt;T[]&gt;&gt; open;
        ///   X&lt;string&gt; closed;
        ///   int closed;
        /// }
        /// </code>
        /// </example>
        public static bool IsOpen(this TypeReference type) =>
            type.Match(
                specializedType => specializedType.TypeArguments.Any(IsOpen),
                arrayType => arrayType.Type.IsOpen(),
                byReferenceType => byReferenceType.Type.IsOpen(),
                pointerType => pointerType.Type.IsOpen(),
                genericParameter => true,
                functionType => functionType.ResultType.IsOpen() || functionType.Params.Any(p => p.Type.IsOpen())
            );

        /// <summary>
        /// Gets whether the type is specialized type with the signature <paramref name="s" />
        /// </summary>
        public static bool IsSpecializationOf(this TypeReference type, TypeSignature s) =>
            type.MatchST(st => st.Type == s, otherwise: _ => false);

        /// <summary>
        /// Gets whether the type is <see cref="System.Nullable{T}" /> - i.e. a value type that is nullable
        /// </summary>
        public static bool IsNullableValueType(this TypeReference type) =>
            type.UnwrapNullableValueType() is object;

        /// <summary>
        /// Gets the T from <see cref="System.Nullable{T}" /> - i.e. a value type that is nullable. Returns null if <paramref name="type" /> is not nullable value type.
        /// </summary>
        public static TypeReference UnwrapNullableValueType(this TypeReference type) =>
            type.MatchST(st => st.Type == TypeSignature.NullableOfT ? st.TypeArguments.Single() : null, otherwise: _ => null);

        /// <summary>
        /// Gets the invoke method for a delegate type.
        /// </summary>
        /// <remarks>
        /// Returns null if the type is not a delegate type; or if the invoke method could not be found.
        /// </remarks>
        public static MethodReference GetDelegateInvokeMethod(this SpecializedType @delegate, MetadataContext cx)
        {
            if (@delegate == null)
                throw new ArgumentNullException("type");
            if (@delegate.Type.Kind == "delegate")
                return cx.GetMemberMethods(@delegate).FirstOrDefault(m => m.Name() == "Invoke");
            else
                return null;
        }

        /// <summary>
        /// Converts a delegate to a matching function type.
        /// </summary>
        /// <remarks>
        /// Returns null if the type is not a delegate type; or if the invoke method could not be found.
        /// </remarks>
        public static FunctionType DelegateToFunction(this SpecializedType @delegate, MetadataContext cx)
        {
            var invoke = @delegate.GetDelegateInvokeMethod(cx);
            return invoke?.ToFunctionType();
        }

        /// <summary> Convers a method reference to a matching function type reference. </summary>
        public static FunctionType ToFunctionType(this MethodReference method) =>
            new FunctionType(method.Params(), method.ResultType());

#if TODO___
        /// <summary>
        /// Gets whether the entity has an attribute of the specified attribute type (or derived attribute types).
        /// </summary>
        /// <param name="entity">The entity on which the attributes are declared.</param>
        /// <param name="attributeType">The attribute type to look for.</param>
        /// <param name="inherit">
        /// Specifies whether attributes inherited from base classes and base members
        /// (if the given <paramref name="entity"/> in an <c>override</c>)
        /// should be returned.
        /// </param>
        public static bool HasAttribute(this IEntity entity, KnownAttribute attributeType, bool inherit=false)
        {
            return GetAttribute(entity, attributeType, inherit) != null;
        }

        /// <summary>
        /// Gets the attribute of the specified attribute type (or derived attribute types).
        /// </summary>
        /// <param name="entity">The entity on which the attributes are declared.</param>
        /// <param name="attributeType">The attribute type to look for.</param>
        /// <param name="inherit">
        /// Specifies whether attributes inherited from base classes and base members
        /// (if the given <paramref name="entity"/> in an <c>override</c>)
        /// should be returned.
        /// </param>
        /// <returns>
        /// Returns the attribute that was found; or <c>null</c> if none was found.
        /// If inherit is true, an from the entity itself will be returned if possible;
        /// and the base entity will only be searched if none exists.
        /// </returns>
        public static IAttribute GetAttribute(this IEntity entity, KnownAttribute attributeType, bool inherit=false)
        {
            return GetAttributes(entity, inherit).FirstOrDefault(a => a.AttributeType.IsKnownType(attributeType));
        }

        /// <summary>
        /// Gets the attributes on the entity.
        /// </summary>
        /// <param name="entity">The entity on which the attributes are declared.</param>
        /// <param name="inherit">
        /// Specifies whether attributes inherited from base classes and base members
        /// (if the given <paramref name="entity"/> in an <c>override</c>)
        /// should be returned.
        /// </param>
        /// <returns>
        /// Returns the list of attributes that were found.
        /// If inherit is true, attributes from the entity itself are returned first;
        /// followed by attributes inherited from the base entity.
        /// </returns>
        public static IEnumerable<IAttribute> GetAttributes(this IEntity entity, bool inherit)
        {
            if (inherit) {
                if (entity is ITypeDefinition td) {
                    return InheritanceHelper.GetAttributes(td);
                } else if (entity is IMember m) {
                    return InheritanceHelper.GetAttributes(m);
                } else {
                    throw new NotSupportedException("Unknown entity type");
                }
            } else {
                return entity.GetAttributes();
            }
        }
        /// <summary>
        /// Gets whether the parameter has an attribute of the specified attribute type (or derived attribute types).
        /// </summary>
        /// <param name="parameter">The parameter on which the attributes are declared.</param>
        /// <param name="attributeType">The attribute type to look for.</param>
        public static bool HasAttribute(this IParameter parameter, KnownAttribute attributeType)
        {
            return GetAttribute(parameter, attributeType) != null;
        }

        /// <summary>
        /// Gets the attribute of the specified attribute type (or derived attribute types).
        /// </summary>
        /// <param name="parameter">The parameter on which the attributes are declared.</param>
        /// <param name="attributeType">The attribute type to look for.</param>
        /// <returns>
        /// Returns the attribute that was found; or <c>null</c> if none was found.
        /// </returns>
        public static IAttribute GetAttribute(this IParameter parameter, KnownAttribute attributeType)
        {
            return parameter.GetAttributes().FirstOrDefault(a => a.AttributeType.IsKnownType(attributeType));
        }
#endif

        /// <summary> Returns the element type of any class implementing <see cref="IEnumerable{T}" /> interface. Returns null when it is not found. </summary>
        public static TypeReference GetElementTypeFromIEnumerable(this TypeReference collectionType, MetadataContext cx, bool allowIEnumerator, out bool? isGeneric)
        {
            bool? isGeneric_ = null;
            var r = collectionType.Match(
                                 st => GetElementTypeFromIEnumerable(st, cx, allowIEnumerator, out isGeneric_),
                                 arr => {
                                     if (arr.Dimensions == 1)
                                     {
                                         isGeneric_ = true;
                                         return arr.Type;
                                     } else return null;
                                 },
                                 _ => null,
                                 _ => null,
                                 _ => null,
                                 _ => null);
            isGeneric = isGeneric_;
            return r;
        }
        /// <summary> Returns the element type of any class implementing <see cref="IEnumerable{T}" /> interface. Returns null when it is not found. </summary>
        public static TypeReference GetElementTypeFromIEnumerable(this SpecializedType collectionType, MetadataContext cx, bool allowIEnumerator, out bool? isGeneric)
        {
            bool foundNonGenericIEnumerable = false;
            foreach (var baseType in cx.GetBaseTypes(collectionType)) {
                if (baseType.Type == TypeSignature.IEnumerableOfT || (allowIEnumerator && baseType.Type == TypeSignature.IEnumeratorOfT)) {
                    isGeneric = true;
                    return Assert.Single(baseType.TypeArguments);
                }
                if (baseType.Type == TypeSignature.IEnumerable || (allowIEnumerator && baseType.Type == TypeSignature.IEnumerator))
                    foundNonGenericIEnumerable = true;
            }
            // System.Collections.IEnumerable found in type hierarchy -> Object is element type.
            if (foundNonGenericIEnumerable) {
                isGeneric = false;
                return TypeSignature.Object;
            }
            isGeneric = null;
            return null;
        }
    }
}
