using System;

namespace Coberec.ExprCS
{
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
    }
}
