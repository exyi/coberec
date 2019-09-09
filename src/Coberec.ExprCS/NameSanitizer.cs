using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Coberec.ExprCS
{
    public static class NameSanitizer
    {
        static bool IsValidChar(char a, bool firstChar)
        {
            if (a == '_') return true;
            switch(char.GetUnicodeCategory(a))
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.LetterNumber:
                    return true;
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.Format:
                    return !firstChar;
                default:
                    return false;
            }
        }
        public static string SanitizeCsharpName(string name, bool? lowerCase = true)
        {
            // keywords are no problem, ILSpy will automatically escape them as @keyword

            // care about characters

            // 1. all surrogates has to be removed, because the C# compiler does not handle that (however, it is allowed by the specification, try to uncomment the following line to check if it's fixed)
            // var a𓀀 = 3;

            // 2. all UTF-16 encodable characters that are not conflict with the spec should remain: https://github.com/donet/csharplang/blob/master/spec/lexical-structure.md#identifiers
            // 3. if the removed character is a whitespace, the next letter will be capitalized to visually split the words
            // 4. if the removed character was visible, it will be replaced by underscore

            var r = new StringBuilder();
            var capitalize = false;
            var insertUnderscore = false;
            var isFirst = true;
            foreach(var c in name)
            {
                if (char.IsSurrogate(c))
                    insertUnderscore = true;
                else if (IsValidChar(c, isFirst))
                {
                    if (insertUnderscore) r.Append('_');
                    r.Append(capitalize ? char.ToUpper(c) : c);
                    capitalize = insertUnderscore = false;
                }
                else if (char.IsWhiteSpace(c))
                    capitalize = true;
                else
                    insertUnderscore = true;
                isFirst = false;
            }
            if (r.Length == 0)
                return "NoName";
            else if (! IsValidChar(r[0], true))
                r.Insert(0, 'M');

            if (lowerCase == true) {
                r[0] = char.ToLower(r[0]);
            } else if (lowerCase == false) {
                r[0] = char.ToUpper(r[0]);
            }
            return r.ToString();
        }

        private static readonly HashSet<string> prohibitedNames = new HashSet<string> {
            // members of every class. We don't want to collide with them.
            // "Finalize", "GetType", "ToString", "Equals", "ReferenceEquals", "GetHashCode", "MemberwiseClone",

            // This leads to some strange behavior, although not forbidden by the spec... https://sharplab.io/#v2:EYLgZgpghgLgrgJwgZwLQGMD2BbADgSwBsIEAfZGBOdGAAgGFaBvAWAChbPaABAZlvwA7OgDcohOBAD6UgNzsAvkA===
            "value__"
        };

        public static string SanitizeMemberName(string name, bool? lowerCase)
        {
            name = SanitizeCsharpName(name, lowerCase);
            if (
                // I'm simply not going to care about these, although they may exist when they don't collide with any property
                name.StartsWith("get_") || name.StartsWith("set_") || name.StartsWith("remove_") || name.StartsWith("add_") || name.StartsWith("op_") || prohibitedNames.Contains(name))
            {
                name = "m" + name;
            }

            return name;
        }

        public static string SanitizeTypeName(string name, bool? lowerCase = false)
        {
            name = SanitizeMemberName(name, lowerCase);
            return name;
        }
    }
}
