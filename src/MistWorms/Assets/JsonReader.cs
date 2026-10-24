using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MistWorms.Assets
{
    /// <summary>
    /// Minimal read-only JSON parser for glTF and manifest.json (Dev/MiniJson is not compiled in Release).
    /// Values are <c>Dictionary&lt;string, object&gt;</c>, <c>List&lt;object&gt;</c>, <c>string</c>, <c>double</c>, <c>bool</c> or null.
    /// </summary>
    internal static class JsonReader
    {
        public static object Parse(string text)
        {
            int i = 0;
            SkipWs(text, ref i);
            object v = ReadValue(text, ref i, 0);
            SkipWs(text, ref i);
            if (i != text.Length) throw Error(i, "trailing characters");
            return v;
        }

        private static FormatException Error(int i, string msg)
        {
            return new FormatException("JSON: " + msg + " at offset " + i);
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }

        private static object ReadValue(string s, ref int i, int depth)
        {
            // Depth cap protects the recursive descent from hostile or corrupt files.
            if (depth > 64) throw Error(i, "nesting too deep");
            if (i >= s.Length) throw Error(i, "unexpected end");
            switch (s[i])
            {
                case '{': return ReadObject(s, ref i, depth);
                case '[': return ReadArray(s, ref i, depth);
                case '"': return ReadString(s, ref i);
                case 't': Expect(s, ref i, "true"); return true;
                case 'f': Expect(s, ref i, "false"); return false;
                case 'n': Expect(s, ref i, "null"); return null;
                default: return ReadNumber(s, ref i);
            }
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error(i, "invalid literal");
            i += word.Length;
        }

        private static object ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            double d;
            if (i == start || !double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw Error(start, "invalid number");
            return d;
        }

        private static string ReadString(string s, ref int i)
        {
            i++; // opening quote
            var sb = new StringBuilder();
            while (true)
            {
                if (i >= s.Length) throw Error(i, "unterminated string");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw Error(i, "unterminated escape");
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        int code;
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            throw Error(i, "invalid \\u escape");
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: sb.Append(e); break; // quote, backslash, slash
                }
            }
        }

        private static List<object> ReadArray(string s, ref int i, int depth)
        {
            var list = new List<object>();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                SkipWs(s, ref i);
                list.Add(ReadValue(s, ref i, depth + 1));
                SkipWs(s, ref i);
                if (i >= s.Length) throw Error(i, "unterminated array");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return list; }
                throw Error(i, "expected ',' or ']'");
            }
        }

        private static Dictionary<string, object> ReadObject(string s, ref int i, int depth)
        {
            var dict = new Dictionary<string, object>();
            i++;
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return dict; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw Error(i, "expected property name");
                string key = ReadString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Error(i, "expected ':'");
                i++;
                SkipWs(s, ref i);
                dict[key] = ReadValue(s, ref i, depth + 1);
                SkipWs(s, ref i);
                if (i >= s.Length) throw Error(i, "unterminated object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return dict; }
                throw Error(i, "expected ',' or '}'");
            }
        }

        // ---------------------------------------------------------------- typed accessors (null/fallback when absent or wrong type)

        public static Dictionary<string, object> Obj(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as Dictionary<string, object> : null;
        }

        public static List<object> Arr(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as List<object> : null;
        }

        public static string Str(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) ? v as string : null;
        }

        public static double Num(object o, string key, double fallback)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) && v is double ? (double)v : fallback;
        }

        public static bool Bool(object o, string key, bool fallback)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) && v is bool ? (bool)v : fallback;
        }

        public static bool Has(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            return d != null && d.ContainsKey(key);
        }
    }
}
