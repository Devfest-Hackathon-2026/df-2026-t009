using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Onsite.Net
{
    // 独自実装。型付き Firestore 値と nullable を失わずに扱う。
    public static class Json
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return new Reader(text).Read();
        }

        public static string Stringify(object value)
        {
            var output = new StringBuilder();
            Write(output, value, 0);
            return output.ToString();
        }

        private static void Write(StringBuilder output, object value, int depth)
        {
            if (depth > 64) throw new FormatException("JSON の入れ子が深すぎます");
            if (value == null) { output.Append("null"); return; }
            if (value is string text) { Quote(output, text); return; }
            if (value is bool flag) { output.Append(flag ? "true" : "false"); return; }
            if (value is IDictionary<string, object> map)
            {
                output.Append('{');
                var first = true;
                foreach (var pair in map)
                {
                    if (!first) output.Append(',');
                    first = false;
                    Quote(output, pair.Key);
                    output.Append(':');
                    Write(output, pair.Value, depth + 1);
                }
                output.Append('}');
                return;
            }
            if (value is IEnumerable list)
            {
                output.Append('[');
                var first = true;
                foreach (var item in list)
                {
                    if (!first) output.Append(',');
                    first = false;
                    Write(output, item, depth + 1);
                }
                output.Append(']');
                return;
            }
            if (value is double d && (double.IsNaN(d) || double.IsInfinity(d)) ||
                value is float f && (float.IsNaN(f) || float.IsInfinity(f)))
                throw new FormatException("JSON の数値は有限である必要があります");
            if (value is byte || value is sbyte || value is short || value is ushort ||
                value is int || value is uint || value is long || value is ulong ||
                value is float || value is double || value is decimal)
            {
                output.Append(((IFormattable)value).ToString(
                    value is double || value is float ? "R" : null, CultureInfo.InvariantCulture));
                return;
            }
            throw new FormatException("JSON に変換できない型です: " + value.GetType().Name);
        }

        private static void Quote(StringBuilder output, string value)
        {
            output.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (c < 32) output.Append("\\u").Append(((int)c).ToString("x4"));
                        else output.Append(c);
                        break;
                }
            }
            output.Append('"');
        }

        private sealed class Reader
        {
            private readonly string source;
            private int offset;

            public Reader(string source) { this.source = source; }

            public object Read()
            {
                var value = Value(0);
                Space();
                if (offset != source.Length) throw Invalid();
                return value;
            }

            private FormatException Invalid() => new FormatException("JSON が不正です (位置 " + offset + ")");
            private void Space()
            {
                while (offset < source.Length &&
                    (source[offset] == ' ' || source[offset] == '\r' ||
                     source[offset] == '\n' || source[offset] == '\t')) offset++;
            }

            private bool Take(char c)
            {
                Space();
                if (offset >= source.Length || source[offset] != c) return false;
                offset++;
                return true;
            }

            private void Require(char c) { if (!Take(c)) throw Invalid(); }

            private object Value(int depth)
            {
                Space();
                if (depth > 64 || offset >= source.Length) throw Invalid();
                var c = source[offset];
                if (c == '"') return Text();
                if (c == '{')
                {
                    offset++;
                    var map = new Dictionary<string, object>(StringComparer.Ordinal);
                    if (Take('}')) return map;
                    do
                    {
                        Space();
                        if (offset >= source.Length || source[offset] != '"') throw Invalid();
                        var key = Text();
                        Require(':');
                        if (map.ContainsKey(key)) throw Invalid();
                        map.Add(key, Value(depth + 1));
                        if (Take('}')) return map;
                        Require(',');
                    } while (true);
                }
                if (c == '[')
                {
                    offset++;
                    var list = new List<object>();
                    if (Take(']')) return list;
                    do
                    {
                        list.Add(Value(depth + 1));
                        if (Take(']')) return list;
                        Require(',');
                    } while (true);
                }
                if (c == 't') { Literal("true"); return true; }
                if (c == 'f') { Literal("false"); return false; }
                if (c == 'n') { Literal("null"); return null; }
                return Number();
            }

            private void Literal(string word)
            {
                if (source.Length - offset < word.Length ||
                    string.CompareOrdinal(source, offset, word, 0, word.Length) != 0) throw Invalid();
                offset += word.Length;
            }

            private string Text()
            {
                Require('"');
                var text = new StringBuilder();
                while (offset < source.Length)
                {
                    var c = source[offset++];
                    if (c == '"') return text.ToString();
                    if (c < 32) throw Invalid();
                    if (c != '\\') { text.Append(c); continue; }
                    if (offset >= source.Length) throw Invalid();
                    switch (source[offset++])
                    {
                        case '"': text.Append('"'); break;
                        case '\\': text.Append('\\'); break;
                        case '/': text.Append('/'); break;
                        case 'b': text.Append('\b'); break;
                        case 'f': text.Append('\f'); break;
                        case 'n': text.Append('\n'); break;
                        case 'r': text.Append('\r'); break;
                        case 't': text.Append('\t'); break;
                        case 'u':
                            if (source.Length - offset < 4 ||
                                !ushort.TryParse(source.Substring(offset, 4), NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture, out var code)) throw Invalid();
                            text.Append((char)code);
                            offset += 4;
                            break;
                        default: throw Invalid();
                    }
                }
                throw Invalid();
            }

            private bool Digit() => offset < source.Length && source[offset] >= '0' && source[offset] <= '9';

            private object Number()
            {
                var start = offset;
                if (offset < source.Length && source[offset] == '-') offset++;
                if (!Digit()) throw Invalid();
                if (source[offset] == '0') offset++;
                else while (Digit()) offset++;
                var integral = true;
                if (offset < source.Length && source[offset] == '.')
                {
                    integral = false;
                    offset++;
                    if (!Digit()) throw Invalid();
                    while (Digit()) offset++;
                }
                if (offset < source.Length && (source[offset] == 'e' || source[offset] == 'E'))
                {
                    integral = false;
                    offset++;
                    if (offset < source.Length && (source[offset] == '+' || source[offset] == '-')) offset++;
                    if (!Digit()) throw Invalid();
                    while (Digit()) offset++;
                }
                var token = source.Substring(start, offset - start);
                if (integral && long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                    return n;
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ||
                    double.IsInfinity(d) || double.IsNaN(d)) throw Invalid();
                return d;
            }
        }
    }

    internal static class Values
    {
        internal static Dictionary<string, object> Map(object value) =>
            value as Dictionary<string, object> ?? throw new FormatException("JSON オブジェクトが必要です");

        internal static object Get(IDictionary<string, object> map, string key) =>
            map.TryGetValue(key, out var value) ? value : null;

        internal static string Text(IDictionary<string, object> map, string key) => Get(map, key) as string;
        internal static double? Number(IDictionary<string, object> map, string key)
        {
            var value = Get(map, key);
            if (value == null) return null;
            if (value is bool || !(value is IConvertible)) throw new FormatException(key + " の数値が不正です");
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        internal static long Long(IDictionary<string, object> map, string key) =>
            OptionalLong(map, key) ?? 0;
        internal static long? OptionalLong(IDictionary<string, object> map, string key)
        {
            var value = Get(map, key);
            if (value == null) return null;
            if (value is long integer) return integer;
            if (value is string text && long.TryParse(text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsed)) return parsed;
            if (value is double number && number == Math.Truncate(number)) return checked((long)number);
            throw new FormatException(key + " の整数が不正です");
        }
        internal static bool Flag(IDictionary<string, object> map, string key) => Get(map, key) is bool value && value;
        internal static DateTime? Time(IDictionary<string, object> map, string key) =>
            Get(map, key) is DateTime value ? value : (DateTime?)null;
    }
}