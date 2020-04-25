using System;
using System.Collections.Generic;
using System.Linq;

namespace Coberec.ExprCS
{
    /// <summary> Collects all settings that affect how the code is generated. </summary>
    public class EmitSettings
    {
        public EmitSettings(bool emitPartialClasses = false, bool sanitizeSymbolNames = true, bool adjustCasing = false)
        {
            this.AdjustCasing = adjustCasing;
            this.EmitPartialClasses = emitPartialClasses;
            this.SanitizeSymbolNames = sanitizeSymbolNames;
        }

        /// <summary> If symbol names should be renamed so that they conform to the .NET coding guidelines (i.e. method names will be capitalized, ...) </summary>
        public bool AdjustCasing { get; }
        /// <summary> If all classes should have the `partial` modifier, so that other code in the same project can add members to them. </summary>
        public bool EmitPartialClasses { get; }
        /// <summary> If symbol names should be sanitized so that they can be used in C# programs. May have some false positives when it renames stuff that would still be valid and in that case you may want to consider disabling this option. </summary>
        public bool SanitizeSymbolNames { get; }
    }
}
