using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public class EmitSettings
    {
        public EmitSettings(bool emitPartialClasses = false, bool sanitizeSymbolNames = true, bool adjustCasing = false)
        {
            this.AdjustCasing = adjustCasing;
            EmitPartialClasses = emitPartialClasses;
            SanitizeSymbolNames = sanitizeSymbolNames;
        }

        public bool AdjustCasing { get; }
        public bool EmitPartialClasses { get; }
        public bool SanitizeSymbolNames { get; }
    }
}
