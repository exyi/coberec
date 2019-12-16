using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Coberec.ExprCS
{
    public partial class FunctionType
    {
        public TypeReference TryGetDelegate(MetadataContext cx = null)
        {
            // TODO: weird delegates (ref parameters, ...), many many arguments
            if (this.ResultType == TypeSignature.Void)
            {
                var reflectionAction = Type.GetType($"System.Action`{this.Params.Length}");
                if (reflectionAction is null)
                    return null;
                var actionSig = TypeSignature.FromType(reflectionAction);
                var actionRef = TypeReference.SpecializedType(actionSig, this.Params.Select(p => p.Type).ToImmutableArray());
                return actionRef;
            }
            else
            {
                var reflectionAction = Type.GetType($"System.Func`{this.Params.Length + 1}");
                if (reflectionAction is null)
                    return null;
                var actionSig = TypeSignature.FromType(reflectionAction);
                var actionRef = TypeReference.SpecializedType(actionSig, this.Params.Select(p => p.Type).Append(this.ResultType).ToImmutableArray());
                return actionRef;
            }
        }

        public override string ToString()
        {
            return $"({string.Join(", ", this.Params)}) -> {this.ResultType}";
        }
    }
}
