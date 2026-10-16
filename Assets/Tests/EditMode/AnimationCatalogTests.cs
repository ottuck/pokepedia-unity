using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pokepedia.Content;
using Pokepedia.Domain;

namespace Pokepedia.Tests
{
    /// <summary>
    /// animation.json 로더(AnimationCatalog)의 파싱·병합·검증을 문자열 픽스처로 확인하고,
    /// 저장소의 실제 Assets/Content/Data/animation.json 을 species.json·moves.json 과 교차 검증한다 (PROJECT.md 5.2, 7.8).
    /// </summary>
    public class AnimationCatalogTests
    {
        // JSON 안의 큰따옴표를 ' 로 적고 Q() 로 바꿔 읽기 쉽게 한다.
        static string Q(string s) => s.Replace('\'', '"');

        const string DefaultsJson = "{ 'windupSec': 0.15, 'lungePx': 10, 'hitShakePx': 2, 'knockbackPx': 0 }";

        static string TypesJson(string skip = null, string waterColor = "'#6890F0'", string extra = "")
        {
            var entries = new List<string>();
            foreach (PokemonType t in Enum.GetValues(typeof(PokemonType)))
            {
                var name = ContentLoader.ToJsonName(t);
                if (name == skip) continue;
                var primary = name == "water" ? waterColor : "'#A8A878'";
                entries.Add("'" + name + "': { 'primary': " + primary + ", 'secondary': '#ffffff' }");
            }
            return "{ " + string.Join(", ", entries) + extra + " }";
        }

        static string Json(string profiles = "{}", string defaults = DefaultsJson, string types = null, string extra = "")
            => Q("{ 'defaults': " + defaults + ", 'profiles': " + profiles + ", 'types': " + (types ?? TypesJson()) + extra + " }");

        static AnimationCatalog Parse(string profiles = "{}", string defaults = DefaultsJson, string types = null)
            => AnimationCatalog.Parse(Json(profiles, defaults, types));

        static FormatException Fails(TestDelegate act) => Assert.Throws<FormatException>(act);

        static FormatException FailsWith(string json, params string[] fragments)
        {
            var ex = Fails(() => AnimationCatalog.Parse(json));
            foreach (var f in fragments) StringAssert.Contains(f, ex.Message);
            return ex;
        }

        // ---------- 정상 파싱 ----------

        [Test]
        public void Defaults_AreRead()
        {
            var d = Parse().Defaults;
            Assert.AreEqual(0.15, d.WindupSec);
            Assert.AreEqual(10, d.LungePx);
            Assert.AreEqual(2, d.HitShakePx);
            Assert.AreEqual(0, d.KnockbackPx);
            CollectionAssert.IsEmpty(d.MoveFx);
        }

        [Test]
        public void Profile_WithAllFields_UsesItsOwnValues()
        {
            var c = Parse("{ 'blastoise': { 'windupSec': 0.35, 'lungePx': 4, 'hitShakePx': 3, 'knockbackPx': 10, 'moveFx': { 'hydro_pump': 'water_cannon' } } }");
            var p = c.ProfileFor("blastoise");
            Assert.AreEqual("blastoise", p.Key);
            Assert.AreEqual(0.35, p.WindupSec);
            Assert.AreEqual(4, p.LungePx);
            Assert.AreEqual(3, p.HitShakePx);
            Assert.AreEqual(10, p.KnockbackPx);
            Assert.AreEqual("water_cannon", p.MoveFx["hydro_pump"]);
        }

        [Test]
        public void Profile_PartialFields_AreMergedWithDefaults()
        {
            var c = Parse("{ 'venusaur': { 'knockbackPx': 6 } }");
            var p = c.ProfileFor("venusaur");
            Assert.AreEqual("venusaur", p.Key);
            Assert.AreEqual(6, p.KnockbackPx);
            Assert.AreEqual(0.15, p.WindupSec);
            Assert.AreEqual(10, p.LungePx);
            Assert.AreEqual(2, p.HitShakePx);
            CollectionAssert.IsEmpty(p.MoveFx);
        }

        [Test]
        public void Profile_Empty_EqualsDefaultsWithItsKey()
        {
            var c = Parse("{ 'gengar': {} }");
            var p = c.ProfileFor("gengar");
            Assert.AreEqual("gengar", p.Key);
            Assert.AreEqual(c.Defaults.WindupSec, p.WindupSec);
            Assert.AreEqual(c.Defaults.LungePx, p.LungePx);
        }

        [Test]
        public void UnknownKey_ReturnsDefaultsUnderThatKey()
        {
            var c = Parse("{ 'pikachu': { 'lungePx': 6 } }");
            var p = c.ProfileFor("missingno");
            Assert.AreEqual("missingno", p.Key);
            Assert.AreEqual(c.Defaults.WindupSec, p.WindupSec);
            Assert.AreEqual(10, p.LungePx);
            Assert.AreEqual(c.Defaults.HitShakePx, p.HitShakePx);
            Assert.AreEqual(c.Defaults.KnockbackPx, p.KnockbackPx);
            CollectionAssert.IsEmpty(p.MoveFx);
        }

        [Test]
        public void ProfileKeys_AndHasProfile_ListOnlyDefinedProfiles()
        {
            var c = Parse("{ 'pikachu': {}, 'gengar': {} }");
            CollectionAssert.AreEquivalent(new[] { "pikachu", "gengar" }, c.ProfileKeys);
            Assert.IsTrue(c.HasProfile("pikachu"));
            Assert.IsFalse(c.HasProfile("mewtwo"));
        }

        [Test]
        public void ProfileFor_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Parse().ProfileFor(null));
        }

        [Test]
        public void MoveFxFor_ReturnsOverrideOrNull()
        {
            var c = Parse("{ 'pikachu': { 'moveFx': { 'thunderbolt': 'lightning_strike' } }, 'gengar': {} }");
            Assert.AreEqual("lightning_strike", c.MoveFxFor("pikachu", "thunderbolt"));
            Assert.IsNull(c.MoveFxFor("pikachu", "tackle"));
            Assert.IsNull(c.MoveFxFor("gengar", "thunderbolt"));
            Assert.IsNull(c.MoveFxFor("missingno", "thunderbolt"));
        }

        [Test]
        public void MoveFx_IsReadOnly()
        {
            var fx = Parse("{ 'pikachu': { 'moveFx': { 'thunderbolt': 'lightning_strike' } } }").ProfileFor("pikachu").MoveFx;
            Assert.IsFalse(fx is IDictionary<string, string> d && !d.IsReadOnly, "MoveFx must not be mutable");
        }

        [Test]
        public void TypeFor_ReturnsColorsAsRgbInts()
        {
            var c = Parse();
            var water = c.TypeFor(PokemonType.Water);
            Assert.AreEqual(PokemonType.Water, water.Type);
            Assert.AreEqual(0x6890F0, water.PrimaryRgb);
            Assert.AreEqual(0xFFFFFF, water.SecondaryRgb);
            foreach (PokemonType t in Enum.GetValues(typeof(PokemonType)))
                Assert.AreEqual(t, c.TypeFor(t).Type);
        }

        [TestCase("#000000", 0x000000)]
        [TestCase("#FFFFFF", 0xFFFFFF)]
        [TestCase("#f8d030", 0xF8D030)]
        [TestCase("#6890F0", 0x6890F0)]
        public void Rgb_ParsesHexColor(string hex, int expected)
        {
            Assert.AreEqual(expected, AnimationCatalog.Rgb(hex));
        }

        [TestCase("6890F0")]
        [TestCase("#6890F")]
        [TestCase("#6890F00")]
        [TestCase("#GG90F0")]
        [TestCase("#68 0F0")]
        [TestCase("")]
        public void Rgb_RejectsMalformed(string hex)
        {
            Fails(() => AnimationCatalog.Rgb(hex));
        }

        [Test]
        public void BoundaryValues_AreAccepted()
        {
            var c = Parse("{ 'lo': { 'windupSec': 0, 'lungePx': 0, 'hitShakePx': 0, 'knockbackPx': 0 }," +
                          "  'hi': { 'windupSec': 2, 'lungePx': 64, 'hitShakePx': 64, 'knockbackPx': 64 } }");
            Assert.AreEqual(0.0, c.ProfileFor("lo").WindupSec);
            Assert.AreEqual(0, c.ProfileFor("lo").LungePx);
            Assert.AreEqual(64, c.ProfileFor("hi").KnockbackPx);
            Assert.AreEqual(2.0, c.ProfileFor("hi").WindupSec);
        }

        // ---------- 오류: 항목 이름이 메시지에 있어야 한다 ----------

        [TestCase("'windupSec': 2.5", "windupSec")]
        [TestCase("'windupSec': -0.1", "windupSec")]
        [TestCase("'lungePx': 65", "lungePx")]
        [TestCase("'lungePx': 1.5", "lungePx")]
        [TestCase("'hitShakePx': -1", "hitShakePx")]
        [TestCase("'knockbackPx': 100", "knockbackPx")]
        [TestCase("'windupSec': '0.5'", "windupSec")]
        [TestCase("'lungePx': null", "lungePx")]
        public void Profile_BadValue_NamesProfileAndField(string field, string fieldName)
        {
            FailsWith(Json("{ 'pikachu': { " + field + " } }"), "pikachu", fieldName);
        }

        [TestCase("'windupSec': 3", "windupSec")]
        [TestCase("'lungePx': -1", "lungePx")]
        public void Defaults_BadValue_NamesDefaultsAndField(string replacement, string fieldName)
        {
            // 같은 키가 두 번 나오면 JsonReader 가 거부하므로 해당 필드를 빼고 다시 넣는다.
            var defaults = DefaultsWithout(fieldName, replacement);
            FailsWith(Json(defaults: defaults), "defaults", fieldName);
        }

        static string DefaultsWithout(string field, string replacement)
        {
            var parts = new List<string>
            {
                "'windupSec': 0.15", "'lungePx': 10", "'hitShakePx': 2", "'knockbackPx': 0",
            };
            parts.RemoveAll(p => p.StartsWith("'" + field + "'", StringComparison.Ordinal));
            if (replacement != null) parts.Add(replacement);
            return "{ " + string.Join(", ", parts) + " }";
        }

        [TestCase("windupSec")]
        [TestCase("lungePx")]
        [TestCase("hitShakePx")]
        [TestCase("knockbackPx")]
        public void Defaults_MissingField_IsRejected(string field)
        {
            FailsWith(Json(defaults: DefaultsWithout(field, null)), "defaults", field);
        }

        [Test]
        public void Defaults_MoveFx_IsRejected()
        {
            FailsWith(Json(defaults: DefaultsWithout("moveFx", "'moveFx': { 'tackle': 'big_hit' }")), "defaults", "moveFx");
        }

        [Test]
        public void UnknownField_InProfile_IsRejected()
        {
            FailsWith(Json("{ 'pikachu': { 'lunge': 6 } }"), "pikachu", "lunge");
        }

        [Test]
        public void Cry_IsRejectedAsUnknownField()
        {
            // 속성 합성음은 울음소리가 아니라 공격 모션 소리라 cry 를 뺐다(진짜 울음소리 설계는 미정).
            FailsWith(Json("{ 'pikachu': { 'cry': { 'semitones': 4, 'length': 0.9 } } }"), "pikachu", "unknown field 'cry'");
            FailsWith(Json(defaults: DefaultsWithout("x", "'cry': { 'semitones': 0, 'length': 1.0 }")), "defaults", "unknown field 'cry'");
        }

        [Test]
        public void UnknownField_InDefaults_IsRejected()
        {
            FailsWith(Json(defaults: DefaultsWithout("x", "'speed': 1")), "defaults", "speed");
        }

        [Test]
        public void UnknownField_AtTopLevel_IsRejected()
        {
            FailsWith(Json(extra: ", 'sounds': {}"), "sounds");
        }

        [Test]
        public void UnknownField_InTypeEntry_IsRejected()
        {
            FailsWith(Json(types: TypesJson().Replace("'secondary': '#ffffff' }", "'secondary': '#ffffff', 'shape': 'ring' }")), "shape");
        }

        [TestCase("defaults")]
        [TestCase("profiles")]
        [TestCase("types")]
        public void MissingSection_IsRejected(string section)
        {
            var parts = new Dictionary<string, string> { ["defaults"] = DefaultsJson, ["profiles"] = "{}", ["types"] = TypesJson() };
            parts.Remove(section);
            var json = Q("{ " + string.Join(", ", parts.Select(kv => "'" + kv.Key + "': " + kv.Value)) + " }");
            FailsWith(json, section);
        }

        [Test]
        public void TopLevel_MustBeObject()
        {
            FailsWith("[]", "animation");
        }

        [Test]
        public void MalformedJson_IsFormatException()
        {
            FailsWith("{ \"defaults\": ", "animation");
        }

        [TestCase("'6890F0'")]
        [TestCase("'#6890F'")]
        [TestCase("'#6890GG'")]
        [TestCase("'blue'")]
        [TestCase("6852080")]
        public void Type_BadColor_NamesTheType(string color)
        {
            FailsWith(Json(types: TypesJson(waterColor: color)), "water", "primary");
        }

        [Test]
        public void Type_Missing_NamesTheType()
        {
            FailsWith(Json(types: TypesJson(skip: "fairy")), "fairy");
        }

        [Test]
        public void Type_Unknown_NamesTheType()
        {
            FailsWith(Json(types: TypesJson(extra: ", 'fyre': { 'primary': '#000000', 'secondary': '#000000' }")), "fyre");
        }

        [Test]
        public void Type_NamesAreLowercaseOnly()
        {
            FailsWith(Json(types: TypesJson(skip: "water", extra: ", 'Water': { 'primary': '#000000', 'secondary': '#000000' }")), "Water");
        }

        [TestCase("'Lightning'")]
        [TestCase("'lightning-strike'")]
        [TestCase("''")]
        [TestCase("1")]
        public void MoveFx_BadValue_NamesProfileAndMove(string value)
        {
            FailsWith(Json("{ 'pikachu': { 'moveFx': { 'thunderbolt': " + value + " } } }"), "pikachu", "thunderbolt");
        }

        [Test]
        public void MoveFx_BadMoveId_IsRejected()
        {
            FailsWith(Json("{ 'pikachu': { 'moveFx': { 'Thunder Bolt': 'lightning_strike' } } }"), "pikachu", "Thunder Bolt");
        }

        [TestCase("Pikachu")]
        [TestCase("mr-mime")]
        public void ProfileKey_MustBeSnakeCase(string key)
        {
            FailsWith(Json("{ '" + key + "': {} }"), key);
        }

        [Test]
        public void Profile_MustBeObject()
        {
            FailsWith(Json("{ 'pikachu': 3 }"), "pikachu");
        }

        // ---------- 실제 데이터 파일 ----------

        static string contentDir;

        static string ContentDir => contentDir ??= FindContentDir();

        // ContentDataFilesTests 와 같은 방식: Unity Test Runner 와 dotnet test 양쪽에서 위로 올라가며 Assets/Content 를 찾는다.
        static string FindContentDir()
        {
            var starts = new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory, AppContext.BaseDirectory };
            foreach (var start in starts)
            {
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Content");
                    if (Directory.Exists(Path.Combine(candidate, "Data"))) return candidate;
                }
            }
            throw new DirectoryNotFoundException("Assets/Content/Data not found above: " + string.Join(", ", starts));
        }

        static string ReadData(string file) => File.ReadAllText(Path.Combine(ContentDir, "Data", file));

        static AnimationCatalog RealCatalog() => AnimationCatalog.Parse(ReadData("animation.json"));

        [Test]
        public void RealFile_PassesSchema()
        {
            var c = RealCatalog();
            Assert.That(c.ProfileKeys.Count, Is.GreaterThan(0));
        }

        [Test]
        public void RealFile_EverySpeciesAnimProfile_IsDefined()
        {
            var c = RealCatalog();
            var missing = ContentLoader.ParseSpecies(ReadData("species.json"))
                .Select(s => s.AnimProfile).Where(k => !c.HasProfile(k)).ToList();
            CollectionAssert.IsEmpty(missing, "animation.json profiles is missing: " + string.Join(", ", missing));
        }

        [Test]
        public void RealFile_AllEighteenTypes_HaveColors()
        {
            var c = RealCatalog();
            var types = (PokemonType[])Enum.GetValues(typeof(PokemonType));
            Assert.AreEqual(18, types.Length);
            foreach (var t in types)
            {
                var v = c.TypeFor(t);
                Assert.AreEqual(t, v.Type);
                Assert.That(v.PrimaryRgb, Is.InRange(0, 0xFFFFFF), t.ToString());
                Assert.That(v.SecondaryRgb, Is.InRange(0, 0xFFFFFF), t.ToString());
            }
        }

        [Test]
        public void RealFile_MoveFxMoveIds_ExistInMoves()
        {
            var c = RealCatalog();
            var moveIds = new HashSet<string>(ContentLoader.ParseMoves(ReadData("moves.json")).Select(m => m.Move.Id));
            foreach (var key in c.ProfileKeys)
                foreach (var moveId in c.ProfileFor(key).MoveFx.Keys)
                    Assert.IsTrue(moveIds.Contains(moveId), $"profile '{key}': moveFx '{moveId}' not in moves.json");
        }

        [Test]
        public void RealFile_Showcase_HasMoveFx()
        {
            // Demo(피카츄 vs 거북왕)의 전용 이펙트 (PROJECT.md 5.2·5.3)
            var c = RealCatalog();
            Assert.AreEqual("lightning_strike", c.MoveFxFor("pikachu", "thunderbolt"));
            Assert.AreEqual("water_cannon", c.MoveFxFor("blastoise", "hydro_pump"));
        }
    }
}
