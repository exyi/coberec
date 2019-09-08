using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    public class EmitSettings
    {
        public EmitSettings(bool emitPartialClasses = false, bool sanitizeSymbolNames = true)
        {
            EmitPartialClasses = emitPartialClasses;
            SanitizeSymbolNames = sanitizeSymbolNames;
        }

        public bool EmitPartialClasses { get; }
        public bool SanitizeSymbolNames { get; }
    }
}
