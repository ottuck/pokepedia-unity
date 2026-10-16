using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Pokepedia.Content
{
    /// <summary>
    /// 콘텐츠 데이터용 작은 JSON 파서 (RFC 8259 문법).
    /// 결과: 객체 → Dictionary&lt;string, object&gt;, 배열 → List&lt;object&gt;, 문자열 → string,
    /// 숫자 → double, true/false → bool, null → null.
    /// Unity 의 JsonUtility 는 Dictionary·최상위 배열을 못 읽고 Content 어셈블리는 UnityEngine 을 쓰지 않으므로 직접 둔다
    /// (Java 라면 Jackson 의 readTree 와 비슷한 역할).
    /// 잘못된 입력·중복 키는 FormatException(위치 포함).
    /// </summary>
    public static class JsonReader
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            var p = new Parser(json);
            p.SkipWs();
            var value = p.ReadValue();
            p.SkipWs();
            if (!p.AtEnd) throw p.Error("unexpected trailing content");
            return value;
        }

        sealed class Parser
        {
            readonly string s;
            int i;

            public Parser(string s) => this.s = s;

            public bool AtEnd => i >= s.Length;

            public FormatException Error(string message) => new FormatException($"JSON: {message} at position {i}");

            public void SkipWs()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                    else break;
                }
            }

            char Peek()
            {
                if (AtEnd) throw Error("unexpected end of input");
                return s[i];
            }

            void Expect(char c)
            {
                if (Peek() != c) throw Error($"'{c}' expected");
                i++;
            }

            public object ReadValue()
            {
                char c = Peek();
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ReadLiteral("true"); return true;
                    case 'f': ReadLiteral("false"); return false;
                    case 'n': ReadLiteral("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber();
                        throw Error($"unexpected character '{c}'");
                }
            }

            Dictionary<string, object> ReadObject()
            {
                Expect('{');
                var map = new Dictionary<string, object>();
                SkipWs();
                if (Peek() == '}') { i++; return map; }
                while (true)
                {
                    SkipWs();
                    if (Peek() != '"') throw Error("property name (string) expected");
                    int keyPos = i;
                    var key = ReadString();
                    if (map.ContainsKey(key))
                    {
                        i = keyPos;
                        throw Error($"duplicate key '{key}'");
                    }
                    SkipWs();
                    Expect(':');
                    SkipWs();
                    map[key] = ReadValue();
                    SkipWs();
                    char c = Peek();
                    if (c == ',') { i++; continue; }
                    if (c == '}') { i++; return map; }
                    throw Error("',' or '}' expected");
                }
            }

            List<object> ReadArray()
            {
                Expect('[');
                var list = new List<object>();
                SkipWs();
                if (Peek() == ']') { i++; return list; }
                while (true)
                {
                    SkipWs();
                    list.Add(ReadValue());
                    SkipWs();
                    char c = Peek();
                    if (c == ',') { i++; continue; }
                    if (c == ']') { i++; return list; }
                    throw Error("',' or ']' expected");
                }
            }

            string ReadString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = s[i];
                    if (c == '"') { i++; return sb.ToString(); }
                    if (c < 0x20) throw Error("control character in string");
                    if (c != '\\') { sb.Append(c); i++; continue; }

                    i++; // '\'
                    if (AtEnd) throw Error("unterminated escape");
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
                        case 'u': sb.Append(ReadHex4()); break;
                        default:
                            i--;
                            throw Error($"invalid escape '\\{e}'");
                    }
                }
            }

            /// <summary>\u 뒤 16진수 4자리. 서로게이트 쌍(😀)은 연속된 두 char 로 그대로 붙이면 된다.</summary>
            char ReadHex4()
            {
                if (i + 4 > s.Length) throw Error("incomplete \\u escape");
                int v = 0;
                for (int k = 0; k < 4; k++)
                {
                    char h = s[i];
                    int d;
                    if (h >= '0' && h <= '9') d = h - '0';
                    else if (h >= 'a' && h <= 'f') d = h - 'a' + 10;
                    else if (h >= 'A' && h <= 'F') d = h - 'A' + 10;
                    else throw Error("hex digit expected in \\u escape");
                    v = v * 16 + d;
                    i++;
                }
                return (char)v;
            }

            void ReadLiteral(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Error($"'{word}' expected");
                i += word.Length;
            }

            /// <summary>JSON 숫자 문법: -? (0 | [1-9][0-9]*) (. [0-9]+)? ([eE] [+-]? [0-9]+)?</summary>
            double ReadNumber()
            {
                int start = i;
                if (s[i] == '-') i++;
                if (AtEnd) throw Error("digit expected");
                if (s[i] == '0') i++;
                else if (IsDigit()) while (IsDigit()) i++;
                else throw Error("digit expected");

                if (!AtEnd && s[i] == '.')
                {
                    i++;
                    if (!IsDigit()) throw Error("digit expected after '.'");
                    while (IsDigit()) i++;
                }
                if (!AtEnd && (s[i] == 'e' || s[i] == 'E'))
                {
                    i++;
                    if (!AtEnd && (s[i] == '+' || s[i] == '-')) i++;
                    if (!IsDigit()) throw Error("digit expected in exponent");
                    while (IsDigit()) i++;
                }
                // "01" 같은 선행 0 은 위에서 '0' 하나만 읽고 끝나므로 뒤의 '1' 이 trailing content 로 걸린다.
                return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            bool IsDigit() => !AtEnd && s[i] >= '0' && s[i] <= '9';
        }
    }
}
