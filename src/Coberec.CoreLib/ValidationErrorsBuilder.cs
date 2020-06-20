using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.CoreLib
{
    /// <summary> Simple incremental builder for <see cref="ValidationErrors" />. The instance is zero-allocation when the result is valid. </summary>
    public struct ValidationErrorsBuilder
    {
        ValidationErrors[] r;
        int index;
        public void Add(ValidationErrors errors)
        {
            if (errors.IsValid()) return;

            if (this.r == null)
            {
                // TODO: pool the array
                this.r = new ValidationErrors[4];
                this.r[0] = errors;
                this.index++;
                return;
            }

            if (this.index == this.r.Length)
            {
                Array.Resize(ref this.r, this.r.Length * 2);
            }
            this.r[this.index] = errors;
            this.index++;
        }

        public void AddErr(string msg) => Add(ValidationErrors.Create(msg));
        public void AddErr(string msg, string f) => Add(ValidationErrors.Create(msg).Nest(f));
        public void AddErr(string msg, string f1, string f2) => Add(ValidationErrors.Create(msg).Nest(f2).Nest(f1));
        public void AddErr(string msg, string f1, string f2, string f3) => Add(ValidationErrors.Create(msg).Nest(f3).Nest(f2).Nest(f1));

        public bool HasErrors => this.r is object;

        public ValidationErrors Build()
        {
            if (this.r == null) return null;
            return ValidationErrors.Join(this.r);
        }
    }
}
