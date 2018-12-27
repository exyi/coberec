using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FsCheck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Coberec.MetaSchema;
using IO = System.IO;

namespace Coberec.Tests.TestGens
{

    public static class FsFunc
    {
        public static Microsoft.FSharp.Core.FSharpFunc<A, B> Create<A, B>(Func<A, B> func) => new Impl<A, B>(func);
        public static Microsoft.FSharp.Core.FSharpFunc<A, Microsoft.FSharp.Core.FSharpFunc<B, C>> Create<A, B, C>(Func<A, B, C> func) => new Impl<A, B, C>(func);

        class Impl<A, B>: Microsoft.FSharp.Core.FSharpFunc<A, B>
        {
            private readonly Func<A, B> x;

            public Impl(Func<A, B> x)
            {
                this.x = x;
            }

            public override B Invoke(A func) => this.x(func);
        }

        class Impl<A, B, C>: Microsoft.FSharp.Core.FSharpFunc<A, Microsoft.FSharp.Core.FSharpFunc<B, C>>
        {
            private readonly Func<A, B, C> x;

            public Impl(Func<A, B, C> x)
            {
                this.x = x;
            }

            public override Microsoft.FSharp.Core.FSharpFunc<B, C> Invoke(A a) => Create<B, C>(b => this.x(a, b));
        }
    }
}
