using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class MethodSignature
    {
        public static MethodSignature Constructor(TypeSignature declaringType, Accessibility accessibility, params MethodParameter[] parameters) =>
            Constructor(declaringType, accessibility, parameters.AsEnumerable());
        public static MethodSignature Constructor(TypeSignature declaringType, Accessibility accessibility, IEnumerable<MethodParameter> parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), ".ctor", TypeSignature.Void, isStatic: false, accessibility, isVirtual: false, isOverride: false, isAbstract: false, hasSpecialName: true, ImmutableArray<GenericParameter>.Empty);

        public static MethodSignature Static(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, params MethodParameter[] parameters) =>
            Static(name, declaringType, accessibility, returnType, parameters.AsEnumerable());
        public static MethodSignature Static(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, IEnumerable<MethodParameter> parameters, IEnumerable<GenericParameter> genericParameters = null) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: true, accessibility, isVirtual: false, isOverride: false, isAbstract: false, hasSpecialName: false, genericParameters?.ToImmutableArray() ?? ImmutableArray<GenericParameter>.Empty);

        public static MethodSignature Instance(string name, TypeSignature declaringType, Accessibility accessibility, TypeReference returnType, params MethodParameter[] parameters) =>
            new MethodSignature(declaringType, parameters.ToImmutableArray(), name, returnType, isStatic: false, accessibility, isVirtual: false, isOverride: false, isAbstract: false, hasSpecialName: false, ImmutableArray<GenericParameter>.Empty);


        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            if (this.Accessibility != Accessibility.APublic) sb.Append(this.Accessibility).Append(" ");
            if (this.IsStatic) sb.Append("static ");
            if (this.HasSpecialName) sb.Append("[specialname] ");
            if (this.IsVirtual && !this.IsOverride) sb.Append("virtual ");
            if (this.IsOverride && !this.IsVirtual) sb.Append("sealed ");
            if (this.IsOverride) sb.Append("override ");
            if (this.IsAbstract) sb.Append("abstract ");
            sb.Append(this.Name);
            if (!this.TypeParameters.IsEmpty)
                sb.Append("<").Append(string.Join(", ", this.TypeParameters)).Append(">");
            sb.Append("(");
            sb.Append(string.Join(", ", this.Params));
            sb.Append("): ");
            sb.Append(this.ResultType);
            return sb.ToString();
        }

    }
}
