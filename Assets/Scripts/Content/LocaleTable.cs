using System;
using System.Collections.Generic;
using System.Text;

namespace Pokepedia.Content
{
    /// <summary>
    /// 로케일 파일(ko/en/ja.json)을 "점으로 이은 키 → 문자열" 표로 읽는다.
    /// 예: { "title": { "start": "시작" } } → "title.start" = "시작"
    /// Unity 의 JsonUtility 는 Dictionary 를 못 읽어서, 문자열·객체만 다루는 작은 파서를 직접 둔다.
    /// </summary>
    public sealed class LocaleTable
    {
        readonly Dictionary<string, string> map;

        LocaleTable(Dictionary<string, string> map) => this.map = map;

        public int Count => map.Count;

        /// <summary>키가 없으면 키 자체를 돌려준다(화면에서 누락이 바로 보이도록).</summary>
        public string Get(string key) => map.TryGetValue(key, out var v) ? v : key;

        public bool Has(string key) => map.ContainsKey(key);

        public IEnumerable<string> Keys => map.Keys;

        /// <summary>"{0}은(는) 쓰러졌다!" 처럼 자리표시자를 채운다.</summary>
        public string Format(string key, params object[] args) => string.Format(Get(key), args);

        public static LocaleTable Parse(string json)
        {
            var map = new Dictionary<string, string>();
            var p = new Parser(json);
            p.SkipWs();
            p.ReadObject("", map);
            return new LocaleTable(map);
        }

        sealed class Parser
        {
            readonly string s;
            int i;

            public Parser(string s) => this.s = s;

            public void SkipWs()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            void Expect(char c)
            {
                SkipWs();
                if (i >= s.Length || s[i] != c) throw new FormatException($"'{c}' expected at {i}");
                i++;
            }

            public void ReadObject(string prefix, Dictionary<string, string> map)
            {
                Expect('{');
                SkipWs();
                if (s[i] == '}') { i++; return; }
                while (true)
                {
                    SkipWs();
                    var key = ReadString();
                    Expect(':');
                    SkipWs();
                    var full = prefix.Length == 0 ? key : prefix + "." + key;
                    if (s[i] == '{') ReadObject(full, map);
                    else if (s[i] == '"') map[full] = ReadString();
                    else throw new FormatException($"only string/object values are allowed (key '{full}')");
                    SkipWs();
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return; }
                    throw new FormatException($"',' or '}}' expected at {i}");
                }
            }

            string ReadString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (i >= s.Length) throw new FormatException("unterminated string");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                        default: sb.Append(e); break; // \" \\ \/
                    }
                }
            }
        }
    }
}
