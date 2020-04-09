using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Coberec.MetaSchema
{
    public sealed class FmtToken
    {
        private readonly ImmutableArray<object> items;
        private readonly int indent = 0;
        private readonly bool isBlock;
        private object tokenMap;

        private FmtToken(int indent, bool isBlock, ImmutableArray<object> items, object tokenMap)
        {
            Debug.Assert(isBlock || indent == 0);
            Debug.Assert(
                tokenMap is null ||
                tokenMap is string ||
                tokenMap is string[] a && a.Length == items.Length);
            this.indent = indent;
            this.isBlock = isBlock;
            this.items = items;
            this.tokenMap = tokenMap;
        }

        static void WriteIndent<TSink>(TSink sink, WriteConfig c)
            where TSink: IFmtTokenSink
        {
            for (int i = 0; i < c.Indent; i++)
                sink.WriteLiteral(c.IndentText, "\\indent");
        }
        public void WriteTo<TSink>(TSink sink, WriteConfig c)
            where TSink: IFmtTokenSink
        {
            c = c.AddIndent(this.indent);

            var newline = string.IsNullOrEmpty(c.IndentText) ? "" : "\n";
            var tokenMapArray = this.tokenMap as string[];
            var tokenMapSingle = this.tokenMap as string;
            if (isBlock)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    var x = items[i];
                    var tokenName = tokenMapSingle ?? tokenMapArray?[i];
                    if (x is FmtToken fr)
                    {
                        if (!fr.isBlock) WriteIndent(sink, c);
                        sink.WriteToken(fr, c, tokenName);
                        if (!fr.isBlock) sink.WriteLiteral(newline, "\\n");
                    }
                    else if (x is FormattableString fs)
                    {
                        WriteIndent(sink, c);
                        sink.WriteFormat(fs.Format, fs.GetArguments(), tokenName);
                        sink.WriteLiteral(newline, "\\n");
                    }
                    else if (x is ITokenFormatable tf)
                    {
                        fr = tf.Format();
                        if (!fr.isBlock) WriteIndent(sink, c);
                        sink.WriteToken(fr, c, tokenName);
                        if (!fr.isBlock) sink.WriteLiteral(newline, "\\n");
                    }
                    else
                    {
                        WriteIndent(sink, c);
                        sink.WriteLiteral(x, tokenName);
                        sink.WriteLiteral(newline, "\\n");
                    }
                }
            }
            else if (items.Length > 0)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    var x = items[i];
                    var tokenName = tokenMapSingle ?? tokenMapArray?[i];
                    if (x is FmtToken fr)
                    {
                        if (fr.isBlock) sink.WriteLiteral(newline, "\\n");
                        sink.WriteToken(fr, c, tokenName);
                    }
                    else if (x is FormattableString fs)
                    {
                        sink.WriteFormat(fs.Format, fs.GetArguments(), tokenName);
                    }
                    else if (x is ITokenFormatable tf)
                    {
                        fr = tf.Format();
                        if (fr.isBlock) sink.WriteLiteral(newline, "\\n");
                        sink.WriteToken(fr, c, tokenName);
                    }
                    else
                    {
                        sink.WriteLiteral(x, tokenName);
                    }
                }
            }
        }

        public void WriteTo(StringBuilder sb, WriteConfig c) => WriteTo(new StringBuilderFmtTokenSink(sb), c);
        public void WriteTo(StringBuilder sb, int indent, string indentText) => WriteTo(sb, new WriteConfig(indent, indentText));

        public static FmtToken Block(object item) => new FmtToken(1, true, ImmutableArray.Create(item), null);
        public static FmtToken Block(params object[] items) => new FmtToken(1, true, items.ToImmutableArray(), null);
        public static FmtToken Block(ImmutableArray<object> items, string[] tokenNames = null) => new FmtToken(1, true, items, tokenNames);
        public static FmtToken Block(IEnumerable<object> items, string[] tokenNames = null) => new FmtToken(1, true, items.ToImmutableArray(), tokenNames);
        public static FmtToken Block(int indent, params object[] items) => new FmtToken(indent, true, items.ToImmutableArray(), null);
        public static FmtToken Single(object item, string tokenName = null) => new FmtToken(0, false, ImmutableArray.Create(item), tokenName);
        public static FmtToken Concat(params object[] items) => new FmtToken(0, false, items.ToImmutableArray(), null);
        public static FmtToken Concat(ImmutableArray<object> items, string[] tokenNames = null) => new FmtToken(0, false, items, tokenNames);
        public static FmtToken Concat(IEnumerable<object> items, string[] tokenNames = null) => new FmtToken(0, false, items.ToImmutableArray(), tokenNames);
        public static FmtToken Join(object separator, IEnumerable<object> items, IEnumerable<string> tokenNames = null)
        {
            var b = ImmutableArray.CreateBuilder<object>();
            var first = true;
            foreach (var x in items)
            {
                if (!first | (first = false))
                    b.Add(separator);

                b.Add(x);
            }
            string[] tokenMap = null;
            if (tokenNames != null)
            {
                tokenMap = new string[b.Count];
                int i = 0;
                first = true;
                foreach (var t in tokenNames)
                {
                    if (i >= tokenMap.Length)
                        break;
                    if (!first | (first = false))
                    {
                        tokenMap[i] = "\\sep";
                        i++;
                    }
                    tokenMap[i] = t;
                    i++;
                }
            }
            return new FmtToken(0, false, b.ToImmutableArray(), tokenMap);
        }

        public static IEnumerable<string> IntegerTokenMap() => Enumerable.Range(0, int.MaxValue).Select(i => i.ToString());

        public FmtToken WithTokenNames(params string[] tokenNames) =>
            new FmtToken(this.indent, this.isBlock, this.items, tokenNames);
        public FmtToken WithIntegerTokenMap() =>
            WithTokenNames(IntegerTokenMap().Take(this.items.Length).ToArray());
        public FmtToken Name(string name) =>
            new FmtToken(0, this.isBlock, ImmutableArray.Create<object>(this), name);

        public static implicit operator FmtToken(string a) => Single(a);
        public static implicit operator FmtToken(FormattableString a) => Single(a);

        public string ToString(string indentText = "\t", int? baseIndent = null)
        {
            var sb = new StringBuilder();
            WriteTo(sb, baseIndent ?? -this.indent, indentText);
            return sb.ToString();
        }
        public override string ToString() => this.ToString("\t", null);
    }

    public readonly struct WriteConfig
    {
        public readonly int Indent;
        public readonly string IndentText;

        public WriteConfig(int indent, string indentText)
        {
            Indent = indent;
            IndentText = indentText;
        }

        public WriteConfig AddIndent(int n = 1) => new WriteConfig(this.Indent + n, this.IndentText);
    }

    public interface IFmtTokenSink
    {
        void WriteFormat(string format, object[] args, string name);
        void WriteLiteral(object text, string name);
        void WriteToken(FmtToken token, WriteConfig config, string name);
    }

    struct StringBuilderFmtTokenSink : IFmtTokenSink
    {
        public readonly StringBuilder sb;

        public StringBuilderFmtTokenSink(StringBuilder sb)
        {
            this.sb = sb;
        }

        public static StringBuilderFmtTokenSink Create() => new StringBuilderFmtTokenSink(new StringBuilder());
        public override string ToString() => sb.ToString();

        public void WriteFormat(string format, object[] args, string name)
        {
            sb.AppendFormat(format, args);
        }

        public void WriteLiteral(object text, string name)
        {
            sb.Append(text);
        }

        public void WriteToken(FmtToken token, WriteConfig config, string name)
        {
            token.WriteTo(this, config);
        }
    }

    public interface ITokenFormatable
    {
        FmtToken Format();
    }
}
