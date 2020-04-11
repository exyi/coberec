using System.Linq;
using Coberec.CSharpGen;

namespace Coberec.ExprCS
{
    /// Fixes types before they are added to the compilation. In particular
    /// * adds implicit constructors
    static class TypeFixer
    {
        public static TypeDef Fix(TypeDef type, MetadataContext cx)
        {
            // note that none of the referenced types must be defined in `cx`. This function is called before the types are committed.

            var kind = type.Signature.Kind;
            if (kind == "class" && !(type.Signature.IsAbstract && !type.Signature.CanOverride) && !type.Members.OfType<MethodDef>().Any(m => m.Signature.IsConstructor()))
            {
                type = type.AddMember(
                    MethodDef.Create(MethodSignature.ImplicitConstructor(type.Signature), @this => @this.Read().Box().CallMethod(MethodSignature.Object_Constructor))
                );
            }

            // apply to descendant types
            type = type.With(members: type.Members.EagerSelect(m => m is TypeDef t ? Fix(t, cx) : m));

            return type;
        }
    }
}
