using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Coberec.CoreLib;
using Coberec.CSharpGen;
using Xunit;

namespace Coberec.ExprCS
{
    /// <summary> Basic metadata about a type - <see cref="Name" />, <see cref="Kind" />, <see cref="Accessibility" />, ... </summary>
    public partial class TypeSignature
    {
        /// <summary> Signature of <see cref="System.Void" /> </summary>
        public static readonly TypeSignature Void = Struct("Void", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Byte" /> </summary>
        public static readonly TypeSignature Byte = Struct("Byte", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.UInt16" /> </summary>
        public static readonly TypeSignature UInt16 = Struct("UInt16", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.UInt32" /> </summary>
        public static readonly TypeSignature UInt32 = Struct("UInt32", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.UInt64" /> </summary>
        public static readonly TypeSignature UInt64 = Struct("UInt64", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.UIntPtr" /> </summary>
        public static readonly TypeSignature UIntPtr = Struct("UIntPtr", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.SByte" /> </summary>
        public static readonly TypeSignature SByte = Struct("SByte", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Int16" /> </summary>
        public static readonly TypeSignature Int16 = Struct("Int16", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Int32" /> </summary>
        public static readonly TypeSignature Int32 = Struct("Int32", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Int64" /> </summary>
        public static readonly TypeSignature Int64 = Struct("Int64", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.IntPtr" /> </summary>
        public static readonly TypeSignature IntPtr = Struct("IntPtr", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Double" /> </summary>
        public static readonly TypeSignature Double = Struct("Double", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Single" /> </summary>
        public static readonly TypeSignature Single = Struct("Single", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.TimeSpan" /> </summary>
        public static readonly TypeSignature TimeSpan = Struct("TimeSpan", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Object" /> </summary>
        public static readonly TypeSignature Object = Class("Object", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Boolean" /> </summary>
        public static readonly TypeSignature Boolean = Struct("Boolean", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.String" /> </summary>
        public static readonly TypeSignature String = SealedClass("String", NamespaceSignature.System, Accessibility.APublic);
        /// <summary> Signature of <see cref="System.Collections.IEnumerable" /> </summary>
        public static readonly TypeSignature IEnumerable = FromType(typeof(System.Collections.IEnumerable));
        /// <summary> Signature of <see cref="System.Collections.IEnumerable" /> </summary>
        public static readonly TypeSignature IEnumerator = FromType(typeof(System.Collections.IEnumerator));
        /// <summary> Signature of <see cref="System.Collections.Generic.IEnumerable{T}" /> </summary>
        public static readonly TypeSignature IEnumerableOfT = FromType(typeof(IEnumerable<>));
        /// <summary> Signature of <see cref="System.Collections.Generic.IEnumerator{T}" /> </summary>
        public static readonly TypeSignature IEnumeratorOfT = FromType(typeof(IEnumerable<>));
        /// <summary> Signature of <see cref="System.Nullable{T}" /> </summary>
        public static readonly TypeSignature NullableOfT = FromType(typeof(Nullable<>));
        /// <summary> Signature of <see cref="System.ValueTuple" /> </summary>
        public static readonly TypeSignature ValueTuple0 = FromType(typeof(ValueTuple));
        /// <summary> Signature of <see cref="System.ValueTuple{T1}" /> </summary>
        public static readonly TypeSignature ValueTuple1 = FromType(typeof(ValueTuple<>));
        /// <summary> Signature of <see cref="System.ValueTuple{T1, T2}" /> </summary>
        public static readonly TypeSignature ValueTuple2 = FromType(typeof(ValueTuple<,>));
        /// <summary> Signature of <see cref="System.ValueTuple{T1, T2, T3}" /> </summary>
        public static readonly TypeSignature ValueTuple3 = FromType(typeof(ValueTuple<,,>));
        /// <summary> Signature of <see cref="System.ValueTuple{T1, T2, T3, T4}" /> </summary>
        public static readonly TypeSignature ValueTuple4 = FromType(typeof(ValueTuple<,,,>));
        /// <summary> Signature of <see cref="System.ValueTuple{T1, T2, T3, T4, T5}" /> </summary>
        public static readonly TypeSignature ValueTuple5 = FromType(typeof(ValueTuple<,,,,>));
        /// <summary> Signature of <see cref="System.ValueTuple{T1, T2, T3, T4, T5, T6}" /> </summary>
        public static readonly TypeSignature ValueTuple6 = FromType(typeof(ValueTuple<,,,,,>));
        /// <summary> Signature of <see cref="System.ValueTuple{T1, T2, T3, T4, T5, T6, T7}" /> </summary>
        public static readonly TypeSignature ValueTuple7 = FromType(typeof(ValueTuple<,,,,,,>));
        /// <summary> Signature of <see cref="System.ValueTuple{T1, T2, T3, T4, T5, T6, T7, TRest}" /> </summary>
        public static readonly TypeSignature ValueTupleRest = FromType(typeof(ValueTuple<,,,,,,,>));
    }
}
