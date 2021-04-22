using System;

namespace Coberec.ExprCS
{
    /// <summary> Extension methods for <see cref="MemberReference" /> interface. </summary>
    public static class MemberReferenceExtensions
    {
        public static string Name(this MemberReference reference) => reference.Signature.Name;
        public static Accessibility Accessibility(this MemberReference reference) => reference.Signature.Accessibility;
        public static TypeReference ResultType(this MemberReference reference) =>
            reference switch {
                MethodReference m => m.ResultType(),
                PropertyReference p => p.Type(),
                FieldReference f => f.ResultType(),
                var x => throw new ArgumentException($"Can not get ResultType of {x.GetType()} - {x}")
            };

        /// <summary> Gets declaring type of the specified member. It is has no declaring type (e.g. a type in namespace), `null` is returned. </summary>
        public static TypeSignature DeclaringType(this MemberSignature member) =>
            member switch {
                MethodSignature m => m.DeclaringType,
                PropertySignature p => p.DeclaringType,
                FieldSignature f => f.DeclaringType,
                TypeSignature t => t.Parent.Match(ns => null, t => t),
                var x => throw new ArgumentException($"Can not get ResultType of {x.GetType()} - {x}")
            };
    }
}
