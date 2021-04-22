using System;
using System.Collections.Generic;
using System.Linq;
using TS = ICSharpCode.Decompiler.TypeSystem;
using R = System.Reflection;
using Xunit;

namespace Coberec.ExprCS
{
    public class SymbolFormatter
    {
        public static string TypeDefToString(Type type) =>
            type.IsGenericType && !type.IsGenericTypeDefinition ? TypeDefToString(type.GetGenericTypeDefinition()) :
            type.FullName;
        public static string TypeDefToString(TS.IType type) =>
            type.GetDefinition().ReflectionName;
        // public static string TypeDefToString(TypeSignature type) =>
        //     type.ReflectionName();
        // public static string TypeToString(TypeReference type) =>
        //     type.Match(
        //         st => st.Type.TypeParameters.Length == 0 ? TypeDefToString
        //     type is TS.ITypeDefinition td ? TypeDefToString(td) :
        //     type is TS.ByReferenceType refType ? TypeToString(refType.ElementType) + "&" :
        //     type is TS.PointerType ptrType ? TypeToString(ptrType.ElementType) + "*" :
        //     type is TS.ArrayType arrType ? TypeToString(arrType.ElementType) + $"[{new string(',', arrType.Dimensions - 1)}]" :
        //     type is  paramType ?  :
        //     type is TS.ITypeParameter typeParam ? "!" + typeParam.Name :
        //     type is TS.Implementation.NullabilityAnnotatedType decoratedType ? TypeToString(decoratedType.TypeWithoutAnnotation) :
        //     throw new NotImplementedException($"Type reference '{type}' of type '{type.GetType().Name}' is not supported.");

        public static string TypeToString(TS.IType type) =>
            type is TS.ITypeDefinition td ? TypeDefToString(td) :
            type is TS.ByReferenceType refType ? TypeToString(refType.ElementType) + "&" :
            type is TS.PointerType ptrType ? TypeToString(ptrType.ElementType) + "*" :
            type is TS.ArrayType arrType ? TypeToString(arrType.ElementType) + $"[{new string(',', arrType.Dimensions - 1)}]" :
            type is TS.ParameterizedType paramType ? TypeDefToString(paramType.GenericType.GetDefinition()) +
                                                     "<" + string.Join(",", paramType.TypeArguments.Select(TypeToString)) + ">" :
            type is TS.ITypeParameter typeParam ? "!" + typeParam.Name :
            type is TS.TupleType tupleType ? TypeToString(tupleType.UnderlyingType) :
            type is TS.Implementation.NullabilityAnnotatedType decoratedType ? TypeToString(decoratedType.TypeWithoutAnnotation) :
            throw new NotImplementedException($"Type reference '{type}' of type '{type.GetType().Name}' is not supported.");
        public static string TypeToString(Type type) =>
            type.IsByRef ? TypeToString(type.GetElementType()) + "&" :
            type.IsPointer ? TypeToString(type.GetElementType()) + "*" :
            type.IsArray ? TypeToString(type.GetElementType()) + $"[{new string(',', type.GetArrayRank() - 1)}]" :
            type.IsGenericType ? TypeDefToString(type.GetGenericTypeDefinition()) +
                                                     "<" + string.Join(",", type.GetGenericArguments().Select(TypeToString)) + ">" :
            type.IsGenericParameter ? "!" + type.Name :
            TypeDefToString(type);
        public static string MethodToString(R.MethodInfo method)
        {
            method = MethodSignature.SanitizeDeclaringTypeGenerics(method);
            if (method.IsGenericMethod && !method.IsGenericMethodDefinition)
                method = method.GetGenericMethodDefinition();
            return
                $"{TypeDefToString(method.DeclaringType)}.{method.Name}({string.Join(",", method.GetParameters().Select(p => TypeToString(p.ParameterType)))})";
        }
        public static string MethodToString(TS.IMethod method)
        {
            var def = (TS.IMethod)method.MemberDefinition;
            return $"{TypeDefToString(def.DeclaringType)}.{def.Name}({string.Join(",", def.Parameters.Select(p => TypeToString(p.Type)))})";
        }
    }
}
