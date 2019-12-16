using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS.Helpers
{
    public static class PropertyBuilders
    {
        public const string AutoPropertyField = "<{0}>k__BackingField";
        public static TypeDef AddAutoProperty(this TypeDef t, string name, TypeReference propertyType, Accessibility accessibility = null, bool isReadOnly = true, bool isStatic = false)
        {
            var (f, p) = CreateAutoProperty(t.Signature, name, propertyType,accessibility, isReadOnly, isStatic);
            return t.AddMember(f, p);

        }
        public static (FieldDef, PropertyDef) CreateAutoProperty(TypeSignature declType, string name, TypeReference propertyType, Accessibility accessibility = null, bool isReadOnly = true, bool isStatic = false)
        {
            accessibility = accessibility ?? Accessibility.APublic;

            var field = new FieldSignature(declType, string.Format(AutoPropertyField, name), Accessibility.APrivate, propertyType, isStatic, isReadOnly);
            var fieldRef = field.SpecializeFromDeclaringType();
            var prop = PropertySignature.Create(name, declType, propertyType, accessibility, isReadOnly ? null : accessibility, isStatic);

            var getter = MethodDef.Create(prop.Getter, thisP => Expression.Dereference(Expression.FieldAccess(fieldRef, thisP)));
            // getter.Attributes.Add(declaringType.Compilation.CompilerGeneratedAttribute());
            var setter = isReadOnly ? null :
                         MethodDef.Create(prop.Setter, (thisP, valueP) => Expression.ReferenceAssign(Expression.FieldAccess(fieldRef, thisP), valueP));
            return (new FieldDef(field),
                    new PropertyDef(prop, getter, setter));
        }
    }
}
