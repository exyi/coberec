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
        public static Func<IL.ILInstruction> InitializeObject(IType expectedType, JToken json)
        {
            switch (json.Type)
            {
                case JTokenType.Integer:
                    if (expectedType.IsKnownType(KnownTypeCode.Int32))
                        return () => new IL.LdcI4(json.Value<int>());
                    else
                        goto default;
                case JTokenType.Float:
                    if (expectedType.IsKnownType(KnownTypeCode.Single))
                        return () => new IL.LdcF4(json.Value<float>());
                    else if (expectedType.IsKnownType(KnownTypeCode.Double))
                        return () => new IL.LdcF8(json.Value<double>());
                    else goto default;
                case JTokenType.String:
                    if (expectedType.IsKnownType(KnownTypeCode.String))
                        return () => new IL.LdStr(json.Value<string>());
                    else
                        goto default;
                case JTokenType.Boolean:
                    if (expectedType.IsKnownType(KnownTypeCode.Boolean))
                        return () => new IL.LdcI4(json.Value<bool>() ? 1 : 0);
                    else
                        goto default;
                case JTokenType.Null:
                    if (expectedType.IsReferenceType == true || expectedType.GetDefinition().IsKnownType(KnownTypeCode.NullableOfT))
                        return () => new IL.LdNull();
                    else
                        goto default;
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
                    throw new ValidationErrorException(ValidationErrors.Create($"Json token of type {json.Type} is not supported when type {expectedType.FullName} is expected."));
            }
        }
    }
}
