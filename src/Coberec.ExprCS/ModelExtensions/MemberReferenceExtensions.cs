namespace Coberec.ExprCS
{
    public static class MemberReferenceExtensions
    {
        public static string Name(this MemberReference reference) => reference.Signature.Name;
        public static Accessibility Accessibility(this MemberReference reference) => reference.Signature.Accessibility;
    }
}
