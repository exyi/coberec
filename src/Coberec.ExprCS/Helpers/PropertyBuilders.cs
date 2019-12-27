using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public static class PropertyBuilders
    {
        public const string AutoPropertyField = "<{0}>k__BackingField";
        /// <summary> Add a C# automatic property to the <paramref name="declaringType" /> (i.e. `public bool X { get; }` or `public string Y { get; set; }`). </summary>
        public static TypeDef AddAutoProperty(this TypeDef declaringType, string name, TypeReference propertyType, Accessibility accessibility = null, bool isReadOnly = true, bool isStatic = false)
        {
            var (f, p) = CreateAutoProperty(declaringType.Signature, name, propertyType,accessibility, isReadOnly, isStatic);
            return declaringType.AddMember(f, p);

        }
        /// <summary> Creates a C# automatic property (i.e. `public bool X { get; }` or `public string Y { get; set; }`). Returns the property and its backing field. </summary>
        public static (FieldDef, PropertyDef) CreateAutoProperty(TypeSignature declType, string name, TypeReference propertyType, Accessibility accessibility = null, bool isReadOnly = true, bool isStatic = false)
        {
            accessibility = accessibility ?? Accessibility.APublic;

            var field = new FieldSignature(declType, string.Format(AutoPropertyField, name), Accessibility.APrivate, propertyType, isStatic, isReadOnly);
            var fieldRef = field.SpecializeFromDeclaringType();
            var prop = PropertySignature.Create(name, declType, propertyType, accessibility, isReadOnly ? null : accessibility, isStatic);

            var getter = MethodDef.Create(prop.Getter, thisP => Expression.FieldAccess(fieldRef, thisP).Dereference());
            // getter.Attributes.Add(declaringType.Compilation.CompilerGeneratedAttribute());
            var setter = isReadOnly ? null :
                         MethodDef.Create(prop.Setter, (thisP, valueP) => Expression.FieldAccess(fieldRef, thisP).ReferenceAssign(valueP));
            return (new FieldDef(field),
                    new PropertyDef(prop, getter, setter));
        }
    }
}
