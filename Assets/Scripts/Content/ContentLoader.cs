using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Pokepedia.Domain;

namespace Pokepedia.Content
{
    /// <summary>
    /// species.json / moves.json / roster.json 문자열 → Domain 객체 + 화면용 정보.
    /// 파일 읽기는 하지 않는다(Unity 에서는 TextAsset 등으로 문자열을 넘긴다).
    /// 스키마가 엄격하다: 누락 필드·모르는 필드·잘못된 값은 어느 항목인지 적은 FormatException.
    /// </summary>
    public static class ContentLoader
    {
        static readonly Regex MoveIdPattern = new Regex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$");

        /// <summary>세 파일을 읽고 교차 검증(로스터 ↔ 종, 종의 타입 ↔ 대표 기술)까지 끝낸 카탈로그.</summary>
        public static ContentCatalog Load(string speciesJson, string movesJson, string rosterJson)
            => new ContentCatalog(ParseSpecies(speciesJson), ParseMoves(movesJson), ParseRoster(rosterJson));

        // ---------- species.json ----------

        static readonly string[] SpeciesFields = { "id", "types", "baseStats", "sprites", "animProfile", "nameKey", "legendary" };
        static readonly string[] StatFields = { "hp", "attack", "defense", "spAttack", "spDefense", "speed" };
        static readonly string[] SpriteFields = { "front", "back", "frontShiny", "backShiny" };

        public static IReadOnlyList<SpeciesInfo> ParseSpecies(string json)
        {
            var result = new List<SpeciesInfo>();
            var seen = new HashSet<int>();
            var items = TopLevelArray(json, "species");
            for (int n = 0; n < items.Count; n++)
            {
                var o = AsObject(items[n], $"species[{n}]");
                o.OnlyFields(SpeciesFields);
                int id = o.Int("id", min: 1);
                o.Name($"species[{n}] (id {id})");
                if (!seen.Add(id)) throw o.Error($"duplicate species id {id}");

                var typeList = o.Array("types");
                if (typeList.Count < 1 || typeList.Count > 2) throw o.Error($"'types' must have 1 or 2 entries (has {typeList.Count})");
                var types = new List<PokemonType>();
                for (int t = 0; t < typeList.Count; t++)
                {
                    var type = ParseType(typeList[t], o.Where + $": types[{t}]");
                    if (types.Contains(type)) throw o.Error($"duplicate type '{ToJsonName(type)}'");
                    types.Add(type);
                }

                var st = o.Object("baseStats");
                st.OnlyFields(StatFields);
                var stats = new StatBlock(
                    st.Int("hp", min: 1), st.Int("attack", min: 1), st.Int("defense", min: 1),
                    st.Int("spAttack", min: 1), st.Int("spDefense", min: 1), st.Int("speed", min: 1));

                var sp = o.Object("sprites");
                sp.OnlyFields(SpriteFields);
                var sprites = new SpriteSet(sp.String("front"), sp.String("back"), sp.String("frontShiny"), sp.String("backShiny"));

                result.Add(new SpeciesInfo(
                    new Species(id, types.AsReadOnly(), stats),
                    o.String("nameKey"), sprites, o.String("animProfile"), o.Bool("legendary")));
            }
            return result.AsReadOnly();
        }

        // ---------- moves.json ----------

        static readonly string[] MoveFields = { "id", "type", "category", "power", "accuracy", "nameKey", "animKind", "role" };

        public static IReadOnlyList<MoveInfo> ParseMoves(string json)
        {
            var result = new List<MoveInfo>();
            var seen = new HashSet<string>();
            var items = TopLevelArray(json, "moves");
            for (int n = 0; n < items.Count; n++)
            {
                var o = AsObject(items[n], $"moves[{n}]");
                o.OnlyFields(MoveFields);
                var id = o.String("id");
                o.Name($"moves[{n}] (id '{id}')");
                if (!MoveIdPattern.IsMatch(id)) throw o.Error("'id' must be snake_case (a-z, 0-9, _)");
                if (!seen.Add(id)) throw o.Error($"duplicate move id '{id}'");

                var type = ParseType(o.Raw("type"), o.Where + ": type");
                var category = o.Enum("category", MoveCategory.Physical, MoveCategory.Special);
                int power = o.Int("power", min: 1);
                int accuracy = o.Int("accuracy", min: 1, max: 100);
                var animKind = o.Enum("animKind", AnimKind.Contact, AnimKind.Projectile, AnimKind.Effect);
                var role = o.Enum("role", MoveRole.Basic, MoveRole.Signature, MoveRole.Secondary);

                result.Add(new MoveInfo(new Move(id, type, category, power, accuracy), o.String("nameKey"), animKind, role));
            }
            return result.AsReadOnly();
        }

        // ---------- roster.json ----------

        static readonly string[] RosterFields = { "id", "tier", "npcWeight" };

        public static IReadOnlyList<RosterEntry> ParseRoster(string json)
        {
            var result = new List<RosterEntry>();
            var seen = new HashSet<int>();
            var items = TopLevelArray(json, "roster");
            for (int n = 0; n < items.Count; n++)
            {
                var o = AsObject(items[n], $"roster[{n}]");
                o.OnlyFields(RosterFields);
                int id = o.Int("id", min: 1);
                o.Name($"roster[{n}] (id {id})");
                if (!seen.Add(id)) throw o.Error($"duplicate roster id {id}");
                var tier = o.Enum("tier", RosterTier.Showcase, RosterTier.Standard);
                int weight = o.Int("npcWeight", min: 0);
                result.Add(new RosterEntry(id, tier, weight));
            }
            return result.AsReadOnly();
        }

        // ---------- 공통 ----------

        /// <summary>JSON 표기: 소문자 영어 (PokemonType.Electric ↔ "electric").</summary>
        public static string ToJsonName(PokemonType type) => type.ToString().ToLowerInvariant();

        static PokemonType ParseType(object raw, string where)
        {
            if (!(raw is string s)) throw new FormatException($"{where}: type name (string) expected");
            foreach (PokemonType t in Enum.GetValues(typeof(PokemonType)))
                if (string.Equals(ToJsonName(t), s, StringComparison.Ordinal)) return t;
            throw new FormatException($"{where}: unknown type '{s}'");
        }

        static List<object> TopLevelArray(string json, string what)
        {
            object root;
            try { root = JsonReader.Parse(json); }
            catch (FormatException e) { throw new FormatException($"{what}: {e.Message}", e); }
            if (!(root is List<object> list)) throw new FormatException($"{what}: top level must be an array");
            return list;
        }

        static Obj AsObject(object raw, string where)
        {
            if (!(raw is Dictionary<string, object> map)) throw new FormatException($"{where}: object expected");
            return new Obj(map, where);
        }

        /// <summary>JSON 객체에서 타입을 확인하며 필드를 꺼내는 도우미. 에러 메시지에 위치(Where)를 붙인다.</summary>
        sealed class Obj
        {
            readonly Dictionary<string, object> map;
            public string Where { get; private set; }

            public Obj(Dictionary<string, object> map, string where)
            {
                this.map = map;
                Where = where;
            }

            public void Name(string where) => Where = where;

            public FormatException Error(string message) => new FormatException($"{Where}: {message}");

            public void OnlyFields(string[] allowed)
            {
                foreach (var key in map.Keys)
                    if (System.Array.IndexOf(allowed, key) < 0)
                        throw Error($"unknown field '{key}' (allowed: {string.Join(", ", allowed)})");
            }

            public object Raw(string name)
            {
                if (!map.TryGetValue(name, out var v)) throw Error($"missing field '{name}'");
                return v;
            }

            public string String(string name)
            {
                if (!(Raw(name) is string s)) throw Error($"'{name}' must be a string");
                if (s.Length == 0) throw Error($"'{name}' must not be empty");
                return s;
            }

            public bool Bool(string name)
            {
                if (!(Raw(name) is bool b)) throw Error($"'{name}' must be true or false");
                return b;
            }

            public int Int(string name, int min = int.MinValue, int max = int.MaxValue)
            {
                if (!(Raw(name) is double d)) throw Error($"'{name}' must be a number");
                if (d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue) throw Error($"'{name}' must be an integer (got {d})");
                int v = (int)d;
                if (v < min || v > max) throw Error($"'{name}' out of range [{min}, {max}] (got {v})");
                return v;
            }

            public List<object> Array(string name)
            {
                if (!(Raw(name) is List<object> list)) throw Error($"'{name}' must be an array");
                return list;
            }

            public Obj Object(string name)
            {
                if (!(Raw(name) is Dictionary<string, object> m)) throw Error($"'{name}' must be an object");
                return new Obj(m, $"{Where}: {name}");
            }

            /// <summary>소문자로 쓴 enum 이름을 읽는다 ("physical" → MoveCategory.Physical).</summary>
            public T Enum<T>(string name, params T[] values) where T : struct
            {
                var s = String(name);
                foreach (var v in values)
                    if (string.Equals(v.ToString().ToLowerInvariant(), s, StringComparison.Ordinal)) return v;
                var names = new List<string>();
                foreach (var v in values) names.Add(v.ToString().ToLowerInvariant());
                throw Error($"'{name}' has unknown value '{s}' (allowed: {string.Join(", ", names)})");
            }
        }
    }
}
