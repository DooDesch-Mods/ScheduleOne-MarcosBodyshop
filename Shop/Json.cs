using System.Globalization;
using System.Text;

namespace MarcosBodyshop.Shop
{
    /// <summary>A small JSON writer for the page's model; the page reads it with JSON.parse.</summary>
    internal sealed class Json
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private bool _first = true;

        internal Json Open(char c) { Comma(); _sb.Append(c); _first = true; return this; }
        internal Json Close(char c) { _sb.Append(c); _first = false; return this; }

        internal Json Key(string k) { Comma(); Str(k); _sb.Append(':'); _first = true; return this; }

        internal Json Val(string s) { Comma(); if (s == null) _sb.Append("null"); else Str(s); _first = false; return this; }
        internal Json Val(int n) { Comma(); _sb.Append(n.ToString(CultureInfo.InvariantCulture)); _first = false; return this; }
        internal Json Val(float f) { Comma(); _sb.Append(f.ToString("0.###", CultureInfo.InvariantCulture)); _first = false; return this; }
        internal Json Val(bool b) { Comma(); _sb.Append(b ? "true" : "false"); _first = false; return this; }

        internal Json Prop(string k, string v) => Key(k).Val(v);
        internal Json Prop(string k, int v) => Key(k).Val(v);
        internal Json Prop(string k, bool v) => Key(k).Val(v);

        private void Comma() { if (!_first) _sb.Append(','); }

        private void Str(string s)
        {
            _sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': _sb.Append(' '); break;
                    default:
                        if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }

        public override string ToString() => _sb.ToString();
    }
}
