using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;

namespace Coberec.CSharpGen.TypeSystem
{
    public sealed class HackedSimpleCompilation : SimpleCompilation, IDecompilerTypeSystem
    {
        public HackedSimpleCompilation(IModuleReference mainAssembly, params IModuleReference[] assemblyReferences) : base(mainAssembly, assemblyReferences)
        {
        }

        public HackedSimpleCompilation(IModuleReference mainAssembly, IEnumerable<IModuleReference> assemblyReferences) : base(mainAssembly, assemblyReferences)
        {
        }
        MetadataModule IDecompilerTypeSystem.MainModule => throw new NotSupportedException("");
    }
}
