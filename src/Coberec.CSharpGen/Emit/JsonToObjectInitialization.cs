using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler.CSharp.Resolver;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;
using Coberec.CSharpGen.TypeSystem;
using IL = ICSharpCode.Decompiler.IL;
using Coberec.CoreLib;
using Coberec.MetaSchema;
using Newtonsoft.Json.Linq;

namespace Coberec.CSharpGen.Emit
{
    public static class JsonToObjectInitialization
    {
        public static IL.ILInstruction InitializeObject(IType expectedType, JToken json)
        {
            switch (json.Type)
            {
                case JTokenType.Integer:
                    return new IL.LdcI4(json.Value<int>());
                case JTokenType.Float:
                    if (expectedType.IsKnownType(KnownTypeCode.Single))
                        return new IL.LdcF4(json.Value<float>());
                    else if (expectedType.IsKnownType(KnownTypeCode.Double))
                        return new IL.LdcF8(json.Value<double>());
                    else goto default;
                case JTokenType.String:
                    return new IL.LdStr(json.Value<string>());
                case JTokenType.Boolean:
                    return new IL.LdcI4(json.Value<bool>() ? 1 : 0);
                case JTokenType.Null:
                    return new IL.LdNull();
                case JTokenType.Undefined:
                case JTokenType.Date:
                case JTokenType.Raw:
                case JTokenType.Bytes:
                case JTokenType.Guid:
                case JTokenType.Uri:
                case JTokenType.TimeSpan:
                case JTokenType.None:
                case JTokenType.Object:
                case JTokenType.Array:
                case JTokenType.Constructor:
                case JTokenType.Property:
                case JTokenType.Comment:
                default:
                    throw new NotSupportedException($"Json token of type {json.Type} is not supported");
            }
        }
    }
}
