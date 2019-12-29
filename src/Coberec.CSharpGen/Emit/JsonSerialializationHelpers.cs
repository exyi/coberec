using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using Coberec.CSharpGen.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.TypeSystem.Implementation;

namespace Coberec.CSharpGen.Emit
{
    public static class JsonSerialializationHelpers
    {
        static IAttribute GetJsonPropertyAttribute(ICompilation compilation, string fieldName)
        {
            var jsonPropType = compilation.FindType(new FullTypeName("Newtonsoft.Json.JsonPropertyAttribute")) ??
                               throw new Exception($"Newtonsoft.Json is probably not referenced, we need it for serialization annotations.");
            var argument = new CustomAttributeTypedArgument<IType>(compilation.FindType(KnownTypeCode.String), fieldName);
            return new DefaultAttribute(jsonPropType, ImmutableArray.Create(argument), ImmutableArray<CustomAttributeNamedArgument<IType>>.Empty);
        }
        public static void AddPropertyAttributes(VirtualProperty property, string fieldName)
        {
            if (ToCamelCase(property.Name) == fieldName) return;

            property.Attributes.Add(GetJsonPropertyAttribute(property.Compilation, fieldName));
        }

        public static void AddParameterAttributes(IMethod ctor, IEnumerable<string> fieldNames)
        {
            Debug.Assert(ctor.IsConstructor);

            foreach (var (p, fName) in ctor.Parameters.ZipTuples(fieldNames))
            {
                if (ToCamelCase(p.Name) != fName)
                {
                    var pCasted = (VirtualParameter)p;
                    pCasted.Attributes.Add(GetJsonPropertyAttribute(ctor.Compilation, fName));
                }
            }
        }


        // copied from https://github.com/JamesNK/Newtonsoft.Json/blob/master/Src/Newtonsoft.Json/Utilities/StringUtils.cs
        static string ToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0]))
            {
                return s;
            }

            char[] chars = s.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                if (i == 1 && !char.IsUpper(chars[i]))
                {
                    break;
                }

                bool hasNext = (i + 1 < chars.Length);
                if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
                {
                    // if the next character is a space, which is not considered uppercase 
                    // (otherwise we wouldn't be here...)
                    // we want to ensure that the following:
                    // 'FOO bar' is rewritten as 'foo bar', and not as 'foO bar'
                    // The code was written in such a way that the first word in uppercase
                    // ends when if finds an uppercase letter followed by a lowercase letter.
                    // now a ' ' (space, (char)32) is considered not upper
                    // but in that case we still want our current character to become lowercase
                    if (char.IsSeparator(chars[i + 1]))
                    {
                        chars[i] = ToLower(chars[i]);
                    }

                    break;
                }

                chars[i] = ToLower(chars[i]);
            }

            return new string(chars);
        }

        private static char ToLower(char c) =>
            char.ToLowerInvariant(c);
    }
}
