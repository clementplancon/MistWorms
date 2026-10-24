#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MistWorms.Dev
{
    /// <summary>Implemented by dev types that know how to turn themselves into a JSON-ready object graph.</summary>
    internal interface IJsonValue
    {
        object ToJson();
    }

    /// <summary>
    /// Dependency-free JSON for the dev bridge. Parsed values are <c>Dictionary&lt;string, object&gt;</c>,
    /// <c>List&lt;object&gt;</c>, <c>string</c>, <c>double</c>, <c>bool</c> or <c>null</c>.
    /// </summary>
    internal static class MiniJson
    {
        // ---------------------------------------------------------------- writer

        public static string Serialize(object value)
        {
            var sb = new StringBuilder(256);
            Write(sb, value, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, int depth)
        {
            // Guards against accidental cycles in ad-hoc object graphs.
            if (depth > 64) { sb.Append("null"); return; }

            switch (value)
            {
                case null: sb.Append("null"); return;
                case string s: WriteString(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case char c: WriteString(sb, c.ToString()); return;
                case float f: WriteFloat(sb, f); return;
                case double d: WriteDouble(sb, d); return;
                case decimal m: sb.Append(m.ToString(CultureInfo.InvariantCulture)); return;
                case int _:
                case long _:
                case short _:
                case byte _:
                case sbyte _:
                case uint _:
                case ulong _:
                case ushort _:
                    sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture)); return;
                case Enum e: WriteString(sb, e.ToString()); return;
                case DateTime dt: WriteString(sb, dt.ToString("o", CultureInfo.InvariantCulture)); return;
                case Vector3 v:
                    sb.Append("{\"x\":"); WriteFloat(sb, v.x);
                    sb.Append(",\"y\":"); WriteFloat(sb, v.y);
                    sb.Append(",\"z\":"); WriteFloat(sb, v.z);
                    sb.Append('}');
                    return;
                case IJsonValue jv: Write(sb, jv.ToJson(), depth + 1); return;
                case IDictionary dict:
                    sb.Append('{');
                    bool firstKey = true;
                    foreach (DictionaryEntry entry in dict)
                    {
                        if (!firstKey) sb.Append(',');
                        firstKey = false;
                        WriteString(sb, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                        sb.Append(':');
                        Write(sb, entry.Value, depth + 1);
                    }
                    sb.Append('}');
                    return;
                case IEnumerable seq:
                    sb.Append('[');
                    bool firstItem = true;
                    foreach (object item in seq)
                    {
                        if (!firstItem) sb.Append(',');
                        firstItem = false;
                        Write(sb, item, depth + 1);
                    }
                    sb.Append(']');
                    return;
                default:
                    WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture));
                    return;
            }
        }

        private static void WriteDouble(StringBuilder sb, double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) { sb.Append("null"); return; }
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }

        // Float round-trip format avoids widening noise such as 0.1f -> 0.10000000149011612.
        private static void WriteFloat(StringBuilder sb, float f)
        {
            if (float.IsNaN(f) || float.IsInfinity(f)) { sb.Append("null"); return; }
            sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------------------------------------------------------- parser

        public static object Parse(string json)
        {
            if (json == null) throw new FormatException("JSON input is null");
            var p = new Parser(json);
            p.SkipWhitespace();
            object value = p.ReadValue(0);
            p.SkipWhitespace();
            if (!p.AtEnd) throw p.Error("trailing characters");
            return value;
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s) { _s = s; }

            public bool AtEnd => _i >= _s.Length;

            public FormatException Error(string msg) => new FormatException($"Invalid JSON at {_i}: {msg}");

            public void SkipWhitespace()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            public object ReadValue(int depth)
            {
                if (depth > 128) throw Error("nesting too deep");
                SkipWhitespace();
                if (AtEnd) throw Error("unexpected end");
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject(depth);
                    case '[': return ReadArray(depth);
                    case '"': return ReadString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error($"unexpected '{c}'");
                }
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0) throw Error($"expected {word}");
                _i += word.Length;
            }

            private Dictionary<string, object> ReadObject(int depth)
            {
                var obj = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++; // {
                SkipWhitespace();
                if (!AtEnd && _s[_i] == '}') { _i++; return obj; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != '"') throw Error("expected key");
                    string key = ReadString();
                    SkipWhitespace();
                    if (AtEnd || _s[_i] != ':') throw Error("expected ':'");
                    _i++;
                    obj[key] = ReadValue(depth + 1);
                    SkipWhitespace();
                    if (AtEnd) throw Error("unterminated object");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == '}') { _i++; return obj; }
                    throw Error("expected ',' or '}'");
                }
            }

            private List<object> ReadArray(int depth)
            {
                var list = new List<object>();
                _i++; // [
                SkipWhitespace();
                if (!AtEnd && _s[_i] == ']') { _i++; return list; }
                while (true)
                {
                    list.Add(ReadValue(depth + 1));
                    SkipWhitespace();
                    if (AtEnd) throw Error("unterminated array");
                    if (_s[_i] == ',') { _i++; continue; }
                    if (_s[_i] == ']') { _i++; return list; }
                    throw Error("expected ',' or ']'");
                }
            }

            private string ReadString()
            {
                _i++; // opening quote
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = _s[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("unterminated escape");
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _s.Length) throw Error("bad unicode escape");
                            sb.Append((char)int.Parse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            _i += 4;
                            break;
                        default: throw Error($"bad escape '\\{e}'");
                    }
                }
            }

            private double ReadNumber()
            {
                int start = _i;
                while (_i < _s.Length && "+-0123456789.eE".IndexOf(_s[_i]) >= 0) _i++;
                if (!double.TryParse(_s.Substring(start, _i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw Error("bad number");
                return d;
            }
        }

        // ---------------------------------------------------------------- typed accessors

        public static Dictionary<string, object> AsObject(object value) => value as Dictionary<string, object>;

        public static bool Has(IDictionary<string, object> o, string key) => o != null && o.TryGetValue(key, out object v) && v != null;

        public static object Get(IDictionary<string, object> o, string key) =>
            o != null && o.TryGetValue(key, out object v) ? v : null;

        public static string GetString(IDictionary<string, object> o, string key, string def = null)
        {
            object v = Get(o, key);
            if (v == null) return def;
            return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static double GetDouble(IDictionary<string, object> o, string key, double def = 0)
        {
            object v = Get(o, key);
            switch (v)
            {
                case double d: return d;
                case bool b: return b ? 1 : 0;
                case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed): return parsed;
                default: return def;
            }
        }

        public static float GetFloat(IDictionary<string, object> o, string key, float def = 0f) => (float)GetDouble(o, key, def);

        public static int GetInt(IDictionary<string, object> o, string key, int def = 0) => (int)Math.Round(GetDouble(o, key, def));

        public static bool GetBool(IDictionary<string, object> o, string key, bool def = false)
        {
            object v = Get(o, key);
            switch (v)
            {
                case bool b: return b;
                case double d: return d != 0;
                case string s when bool.TryParse(s, out bool parsed): return parsed;
                case string s when s == "1": return true;
                case string s when s == "0": return false;
                default: return def;
            }
        }

        public static List<object> GetList(IDictionary<string, object> o, string key) => Get(o, key) as List<object>;

        public static Dictionary<string, object> GetObject(IDictionary<string, object> o, string key) => Get(o, key) as Dictionary<string, object>;
    }
}
#endif
