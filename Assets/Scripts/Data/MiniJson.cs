using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RecreioEspacial.Data
{
    /// <summary>
    /// Leitor de JSON mínimo, sem dependências.
    /// Devolve: Dictionary&lt;string, object&gt;, List&lt;object&gt;, string, double, bool ou null.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            var p = new Parser(json);
            p.SkipWhitespace();
            object value = p.ParseValue();
            p.SkipWhitespace();
            if (!p.AtEnd) throw p.Error("conteúdo extra depois do fim do JSON");
            return value;
        }

        class Parser
        {
            readonly string s;
            int i;

            public Parser(string text) { s = text ?? ""; }

            public bool AtEnd => i >= s.Length;

            public FormatException Error(string msg)
            {
                int line = 1;
                for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
                return new FormatException($"JSON inválido (linha {line}): {msg}");
            }

            public void SkipWhitespace()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            public object ParseValue()
            {
                SkipWhitespace();
                if (AtEnd) throw Error("fim inesperado");
                char c = s[i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': Expect("true"); return true;
                    case 'f': Expect("false"); return false;
                    case 'n': Expect("null"); return null;
                    default:
                        if (c == '-' || char.IsDigit(c)) return ParseNumber();
                        throw Error($"caractere inesperado '{c}'");
                }
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error($"esperava '{word}'");
                i += word.Length;
            }

            Dictionary<string, object> ParseObject()
            {
                var obj = new Dictionary<string, object>();
                i++; // {
                SkipWhitespace();
                if (i < s.Length && s[i] == '}') { i++; return obj; }
                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || s[i] != '"') throw Error("esperava nome de campo");
                    string key = ParseString();
                    SkipWhitespace();
                    if (AtEnd || s[i] != ':') throw Error("esperava ':'");
                    i++;
                    obj[key] = ParseValue();
                    SkipWhitespace();
                    if (AtEnd) throw Error("objeto não fechado");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return obj; }
                    throw Error("esperava ',' ou '}'");
                }
            }

            List<object> ParseArray()
            {
                var list = new List<object>();
                i++; // [
                SkipWhitespace();
                if (i < s.Length && s[i] == ']') { i++; return list; }
                while (true)
                {
                    list.Add(ParseValue());
                    SkipWhitespace();
                    if (AtEnd) throw Error("lista não fechada");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return list; }
                    throw Error("esperava ',' ou ']'");
                }
            }

            string ParseString()
            {
                var sb = new StringBuilder();
                i++; // "
                while (true)
                {
                    if (AtEnd) throw Error("texto não fechado");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("escape incompleto");
                    char e = s[i++];
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
                            if (i + 4 > s.Length) throw Error("escape \\u incompleto");
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                            break;
                        default: throw Error($"escape desconhecido '\\{e}'");
                    }
                }
            }

            double ParseNumber()
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                string num = s.Substring(start, i - start);
                if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw Error($"número inválido '{num}'");
                return d;
            }
        }
    }
}
