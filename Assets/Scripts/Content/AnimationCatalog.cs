using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Pokepedia.Domain;

namespace Pokepedia.Content
{
    /// <summary>울음소리 합성 인자: 반음 오프셋(체급이 클수록 낮게), 길이 배수 (PROJECT.md 8.3)</summary>
    public sealed record CryParams(double Semitones, double Length);

    /// <summary>
    /// 종(species.json 의 animProfile 키)별 연출 데이터 (PROJECT.md 5.2: 포켓몬별 애니메이션을 만들지 않고 강도·속도를 데이터로 조정).
    /// MoveFx: 기술 id → 전용 이펙트 id (Showcase 오버라이드, 예: pikachu 의 thunderbolt → "lightning_strike").
    /// 없으면 화면이 기술의 animKind(접촉/발사/효과) 기본 타임라인을 쓴다.
    /// </summary>
    public sealed record AnimationProfile(
        string Key,
        CryParams Cry,
        double WindupSec,
        int LungePx,
        int HitShakePx,
        int KnockbackPx,
        IReadOnlyDictionary<string, string> MoveFx);

    /// <summary>타입별 이펙트 색 (0xRRGGBB). 공통 이펙트 프리팹 하나 + 타입별 색 파라미터 (PROJECT.md 5.2).</summary>
    public sealed record TypeVisual(PokemonType Type, int PrimaryRgb, int SecondaryRgb);

    /// <summary>
    /// Assets/Content/Data/animation.json 로더. 순수 C#.
    /// 구조: { "defaults": {...}, "profiles": { key: {...} }, "types": { type: { primary, secondary } } }.
    /// defaults 는 moveFx 를 뺀 모든 필드가 필수(moveFx 는 종 단위 오버라이드라 defaults 에 둘 수 없다),
    /// profiles 의 필드는 모두 선택이며 빠진 필드는 defaults 로 채운다(cry 안의 semitones/length 도 각각).
    /// ContentLoader 와 같이 엄격하다: 누락·모르는 필드·범위 밖 값은 어느 항목인지 적은 FormatException.
    /// </summary>
    public sealed class AnimationCatalog
    {
        public const double MaxWindupSec = 2;
        public const int MaxPx = 64;
        public const double MinCryLength = 0.3;
        public const double MaxCryLength = 3;
        public const double MaxCrySemitones = 24;

        static readonly Regex SnakeCase = new Regex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$");
        static readonly Regex HexColor = new Regex("^#[0-9A-Fa-f]{6}$");

        static readonly string[] TopFields = { "defaults", "profiles", "types" };
        static readonly string[] DefaultsFields = { "cry", "windupSec", "lungePx", "hitShakePx", "knockbackPx" };
        static readonly string[] ProfileFields = { "cry", "windupSec", "lungePx", "hitShakePx", "knockbackPx", "moveFx" };
        static readonly string[] CryFields = { "semitones", "length" };
        static readonly string[] TypeFields = { "primary", "secondary" };

        static readonly IReadOnlyDictionary<string, string> NoMoveFx =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        readonly Dictionary<string, AnimationProfile> profiles;
        readonly Dictionary<PokemonType, TypeVisual> types;

        AnimationCatalog(AnimationProfile defaults, Dictionary<string, AnimationProfile> profiles, Dictionary<PokemonType, TypeVisual> types)
        {
            Defaults = defaults;
            this.profiles = profiles;
            this.types = types;
        }

        public static AnimationCatalog Parse(string json)
        {
            object root;
            try { root = JsonReader.Parse(json); }
            catch (FormatException e) { throw new FormatException($"animation: {e.Message}", e); }
            if (!(root is Dictionary<string, object> map)) throw new FormatException("animation: top level must be an object");

            var top = new Obj(map, "animation");
            top.OnlyFields(TopFields);

            var defaults = ParseDefaults(top.Object("defaults", "animation: defaults"));

            var profiles = new Dictionary<string, AnimationProfile>(StringComparer.Ordinal);
            var profilesObj = top.Object("profiles", "animation: profiles");
            foreach (var key in profilesObj.Keys)
            {
                var where = $"animation: profiles.{key}";
                if (!SnakeCase.IsMatch(key)) throw new FormatException($"{where}: profile key must be snake_case (a-z, 0-9, _)");
                profiles.Add(key, ParseProfile(key, profilesObj.Object(key, where), defaults));
            }

            var types = ParseTypes(top.Object("types", "animation: types"));
            return new AnimationCatalog(defaults, profiles, types);
        }

        /// <summary>프로필 키가 없을 때 쓰는 기본값 (Key 는 "defaults", MoveFx 는 비어 있음)</summary>
        public AnimationProfile Defaults { get; }

        /// <summary>animation.json 의 profiles 에 정의된 키 목록</summary>
        public IReadOnlyCollection<string> ProfileKeys => profiles.Keys;

        /// <summary>profiles 에 key 가 정의되어 있는지 (ProfileFor 는 없는 키에도 기본값을 주므로 데이터 검증용)</summary>
        public bool HasProfile(string key) => key != null && profiles.ContainsKey(key);

        /// <summary>key 의 프로필(빠진 필드는 기본값으로 채움). 모르는 키면 기본값을 key 이름으로 돌려준다.</summary>
        public AnimationProfile ProfileFor(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return profiles.TryGetValue(key, out var p) ? p : Defaults with { Key = key };
        }

        /// <summary>18타입 모두 정의되어 있어야 한다.</summary>
        public TypeVisual TypeFor(PokemonType type)
        {
            if (types.TryGetValue(type, out var v)) return v;
            throw new ArgumentOutOfRangeException(nameof(type), type, "unknown type");
        }

        /// <summary>전용 이펙트 id, 없으면 null</summary>
        public string MoveFxFor(string profileKey, string moveId)
        {
            if (profileKey == null || moveId == null) return null;
            if (!profiles.TryGetValue(profileKey, out var p)) return null;
            return p.MoveFx.TryGetValue(moveId, out var fx) ? fx : null;
        }

        /// <summary>"#RRGGBB"(대소문자 무관) → 0xRRGGBB. 형식이 틀리면 FormatException.</summary>
        public static int Rgb(string hex)
        {
            if (hex == null) throw new ArgumentNullException(nameof(hex));
            if (!HexColor.IsMatch(hex)) throw new FormatException($"color must be #RRGGBB (got '{hex}')");
            return int.Parse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        // ---------- 파싱 ----------

        static AnimationProfile ParseDefaults(Obj o)
        {
            o.OnlyFields(DefaultsFields);
            var cry = o.Object("cry", o.Where + ": cry");
            cry.OnlyFields(CryFields);
            return new AnimationProfile(
                "defaults",
                new CryParams(Semitones(cry), CryLength(cry)),
                WindupSec(o), Px(o, "lungePx"), Px(o, "hitShakePx"), Px(o, "knockbackPx"),
                NoMoveFx);
        }

        static AnimationProfile ParseProfile(string key, Obj o, AnimationProfile d)
        {
            o.OnlyFields(ProfileFields);

            var cryParams = d.Cry;
            if (o.Has("cry"))
            {
                var cry = o.Object("cry", o.Where + ": cry");
                cry.OnlyFields(CryFields);
                cryParams = new CryParams(
                    cry.Has("semitones") ? Semitones(cry) : d.Cry.Semitones,
                    cry.Has("length") ? CryLength(cry) : d.Cry.Length);
            }

            var moveFx = NoMoveFx;
            if (o.Has("moveFx"))
            {
                var fxObj = o.Object("moveFx", o.Where + ": moveFx");
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var moveId in fxObj.Keys)
                {
                    if (!SnakeCase.IsMatch(moveId)) throw fxObj.Error($"move id '{moveId}' must be snake_case (a-z, 0-9, _)");
                    var fx = fxObj.String(moveId);
                    if (!SnakeCase.IsMatch(fx)) throw fxObj.Error($"'{moveId}' effect id must be lowercase snake_case (got '{fx}')");
                    map.Add(moveId, fx);
                }
                moveFx = new ReadOnlyDictionary<string, string>(map);
            }

            return new AnimationProfile(
                key,
                cryParams,
                o.Has("windupSec") ? WindupSec(o) : d.WindupSec,
                o.Has("lungePx") ? Px(o, "lungePx") : d.LungePx,
                o.Has("hitShakePx") ? Px(o, "hitShakePx") : d.HitShakePx,
                o.Has("knockbackPx") ? Px(o, "knockbackPx") : d.KnockbackPx,
                moveFx);
        }

        static Dictionary<PokemonType, TypeVisual> ParseTypes(Obj o)
        {
            var byName = new Dictionary<string, PokemonType>(StringComparer.Ordinal);
            foreach (PokemonType t in Enum.GetValues(typeof(PokemonType))) byName.Add(ContentLoader.ToJsonName(t), t);

            var result = new Dictionary<PokemonType, TypeVisual>();
            foreach (var name in o.Keys)
            {
                var where = $"{o.Where}.{name}";
                if (!byName.TryGetValue(name, out var type)) throw new FormatException($"{where}: unknown type '{name}'");
                var e = o.Object(name, where);
                e.OnlyFields(TypeFields);
                result.Add(type, new TypeVisual(type, Color(e, "primary"), Color(e, "secondary")));
            }

            var missing = new List<string>();
            foreach (var kv in byName)
                if (!result.ContainsKey(kv.Value)) missing.Add(kv.Key);
            if (missing.Count > 0) throw o.Error($"missing type(s): {string.Join(", ", missing)} (all {byName.Count} types are required)");
            return result;
        }

        static double Semitones(Obj cry) => cry.Double("semitones", -MaxCrySemitones, MaxCrySemitones);
        static double CryLength(Obj cry) => cry.Double("length", MinCryLength, MaxCryLength);
        static double WindupSec(Obj o) => o.Double("windupSec", 0, MaxWindupSec);
        static int Px(Obj o, string name) => o.Int(name, 0, MaxPx);

        static int Color(Obj o, string name)
        {
            var s = o.String(name);
            if (!HexColor.IsMatch(s)) throw o.Error($"'{name}' must be a #RRGGBB color (got '{s}')");
            return Rgb(s);
        }

        /// <summary>JSON 객체에서 타입을 확인하며 필드를 꺼내는 도우미. 에러 메시지에 위치(Where)를 붙인다 (ContentLoader.Obj 와 같은 방식).</summary>
        sealed class Obj
        {
            readonly Dictionary<string, object> map;
            public string Where { get; }

            public Obj(Dictionary<string, object> map, string where)
            {
                this.map = map;
                Where = where;
            }

            public IEnumerable<string> Keys => map.Keys;

            public bool Has(string name) => map.ContainsKey(name);

            public FormatException Error(string message) => new FormatException($"{Where}: {message}");

            public void OnlyFields(string[] allowed)
            {
                foreach (var key in map.Keys)
                    if (Array.IndexOf(allowed, key) < 0)
                        throw Error($"unknown field '{key}' (allowed: {string.Join(", ", allowed)})");
            }

            object Raw(string name)
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

            public double Double(string name, double min, double max)
            {
                if (!(Raw(name) is double d)) throw Error($"'{name}' must be a number");
                if (d < min || d > max) throw Error($"'{name}' out of range [{Num(min)}, {Num(max)}] (got {Num(d)})");
                return d;
            }

            public int Int(string name, int min, int max)
            {
                if (!(Raw(name) is double d)) throw Error($"'{name}' must be a number");
                if (d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue) throw Error($"'{name}' must be an integer (got {Num(d)})");
                int v = (int)d;
                if (v < min || v > max) throw Error($"'{name}' out of range [{min}, {max}] (got {v})");
                return v;
            }

            public Obj Object(string name, string where)
            {
                if (!(Raw(name) is Dictionary<string, object> m)) throw Error($"'{name}' must be an object");
                return new Obj(m, where);
            }

            static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
