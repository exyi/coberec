using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Coberec.MetaSchema
{
    public sealed class FormatResult
    {
        private readonly ImmutableArray<object> items;
        private readonly int indent = 0;
        private readonly bool isBlock;

        private FormatResult(int indent, bool isBlock, ImmutableArray<object> items)
        {
            Debug.Assert(isBlock || indent == 0);
            this.indent = indent;
            this.isBlock = isBlock;
            this.items = items;
        }


        static StringBuilder AppendIndent(StringBuilder sb, int indent, string indentText)
        {
            for (int i = 0; i < indent; i++)
                sb.Append(indentText);
            return sb;
        }
        public void WriteTo(StringBuilder sb, int indent, string indentText)
        {
            indent = indent + this.indent;

            var newline = string.IsNullOrEmpty(indentText) ? "" : "\n";
            if (isBlock)
            {
                foreach (var x in this.items)
                {
                    if (x is FormatResult fr)
                    {
                        if (!fr.isBlock) AppendIndent(sb, indent, indentText);
                        fr.WriteTo(sb, (int)indent, indentText);
                        if (!fr.isBlock) sb.Append(newline);
                    }
                    else if (x is FormattableString fs)
                        AppendIndent(sb, indent, indentText).AppendFormat(fs.Format, fs.GetArguments()).Append(newline);
                    else
                        AppendIndent(sb, indent, indentText).Append(x).Append(newline);
                }
            }
            else if (items.Length > 0)
            {
                foreach (var x in this.items)
                {
                    if (x is FormatResult fr)
                    {
                        if (fr.isBlock) sb.Append(newline);
                        fr.WriteTo(sb, (int)indent, indentText);
                    }
                    else if (x is FormattableString fs)
                        sb.AppendFormat(fs.Format, fs.GetArguments());
                    else
                        sb.Append(x);
                }
            }
        }

        public static FormatResult Block(object item) => new FormatResult(1, true, ImmutableArray.Create(item));
        public static FormatResult Block(params object[] items) => new FormatResult(1, true, items.ToImmutableArray());
        public static FormatResult Block(IEnumerable<object> items) => new FormatResult(1, true, items.ToImmutableArray());
        public static FormatResult Block(int indent, params object[] items) => new FormatResult(indent, true, items.ToImmutableArray());
        public static FormatResult Concat(object item) => new FormatResult(0, false, ImmutableArray.Create(item));
        public static FormatResult Concat(params object[] items) => new FormatResult(0, false, items.ToImmutableArray());
        public static FormatResult Concat(IEnumerable<object> items) => new FormatResult(0, false, items.ToImmutableArray());
        public static FormatResult Join(object separator, IEnumerable<object> items)
        {
            var b = ImmutableArray.CreateBuilder<object>();
            var first = true;
            foreach (var x in items)
            {
                if (!first | (first = false))
                    b.Add(separator);

                b.Add(x);
            }
            return new FormatResult(0, false, b.ToImmutableArray());
        }

        public static implicit operator FormatResult(string a) => Concat(a);
        public static implicit operator FormatResult(FormattableString a) => Concat(a);

        public string ToString(string indentText = "\t", int? baseIndent = null)
        {
            var sb = new StringBuilder();
            WriteTo(sb, baseIndent ?? -this.indent, indentText);
            return sb.ToString();
        }
        public override string ToString() => this.ToString("\t", null);
    }

    public sealed class GraphqlJsonWriter : Newtonsoft.Json.JsonTextWriter
    {
        public GraphqlJsonWriter(System.IO.TextWriter textWriter) : base(textWriter)
        {
            QuoteName = false;
            Formatting = Newtonsoft.Json.Formatting.None;
            // FloatFormatHandling = Newtonsoft.Json.FloatFormatHandling.
        }

        public override void WritePropertyName(string name, bool escape)
        {
            base.WritePropertyName(name, false);
        }
    }
}
