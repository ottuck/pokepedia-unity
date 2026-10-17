using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pokepedia.Content;
using Pokepedia.Domain;

namespace Pokepedia.Tests
{
    /// <summary>
    /// animation.json 로더(AnimationCatalog)의 병합·검증을 문자열 픽스처로 확인하고,
    /// 저장소의 실제 animation.json 을 species.json·moves.json 과 교차 검증한다 (PROJECT.md 5.2, 7.8).
    /// </summary>
    public class AnimationCatalogTests
    {
        // JSON 안의 큰따옴표를 ' 로 적고 Q() 로 바꿔 읽기 쉽게 한다.
        static string Q(string s) => s.Replace('\'', '"');

        const string DefaultsJson = "{ 'windupSec': 0.15, 'lungePx': 10, 'hitShakePx': 2, 'knockbackPx': 0 }";

        static string TypesJson(string skip = null, string waterColor = "'#6890F0'")
        {
            var entries = new List<string>();
            foreach (PokemonType t in Enum.GetValues(typeof(PokemonType)))
            {
                var name = ContentLoader.ToJsonName(t);
                if (name == skip) continue;
                var primary = name == "water" ? waterColor : "'#A8A878'";
                entries.Add("'" + name + "': { 'primary': " + primary + ", 'secondary': '#ffffff' }");
            }
            return "{ " + string.Join(", ", entries) + " }";
        }

        static string Json(string profiles = "{}", string defaults = DefaultsJson, string types = null, string moveSfx = null)
            => Q("{ 'defaults': " + defaults + ", 'profiles': " + profiles + ", 'types': " + (types ?? TypesJson())
                 + (moveSfx == null ? "" : ", 'moveSfx': " + moveSfx) + " }");

        static AnimationCatalog Parse(string profiles = "{}") => AnimationCatalog.Parse(Json(profiles));

        static void FailsWith(string json, params string[] fragments)
        {
            var ex = Assert.Throws<FormatException>(() => AnimationCatalog.Parse(json));
            foreach (var f in fragments) StringAssert.Contains(f, ex.Message);
        }

        // ---------- 병합·조회 ----------

        [Test]
        public void Profile_PartialFields_AreMergedWithDefaults()
        {
            var p = Parse("{ 'venusaur': { 'knockbackPx': 6 } }").ProfileFor("venusaur");
            Assert.AreEqual(6, p.KnockbackPx);
            Assert.AreEqual(0.15, p.WindupSec);
            Assert.AreEqual(10, p.LungePx);
            Assert.AreEqual(2, p.HitShakePx);
            Assert.IsNull(p.Mouth);   // 입 위치는 기본값이 없다 (없으면 화면이 몸 기준 위치를 쓴다)
        }

        [Test]
        public void Mouth_IsFractionOfSpriteFromCenter()
        {
            var p = Parse("{ 'blastoise': { 'mouth': { 'x': -0.47, 'y': -0.18 } } }").ProfileFor("blastoise");
            Assert.AreEqual(new SpriteAnchor(-0.47, -0.18), p.Mouth);
        }

        [Test]
        public void UnknownKey_FallsBackToDefaults()
        {
            var p = Parse("{ 'pikachu': { 'lungePx': 6 } }").ProfileFor("missingno");
            Assert.AreEqual("missingno", p.Key);
            Assert.AreEqual(10, p.LungePx);
            CollectionAssert.IsEmpty(p.MoveFx);
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
        public void MoveSfxFor_ReturnsSoundIdOrNull_AndRejectsBadId()
        {
            var c = AnimationCatalog.Parse(Json(moveSfx: "{ 'water_pledge': 'water_magic' }"));
            Assert.AreEqual("water_magic", c.MoveSfxFor("water_pledge"));
            Assert.IsNull(c.MoveSfxFor("thunder"));                    // 없으면 화면이 타입 공격음을 쓴다
            Assert.IsNull(Parse().MoveSfxFor("water_pledge"));         // moveSfx 는 선택 항목
            FailsWith(Json(moveSfx: "{ 'tackle': 'Tackle' }"), "moveSfx", "tackle");
        }

        [Test]
        public void TypeFor_ReturnsColorsAsRgbInts()
        {
            var water = Parse().TypeFor(PokemonType.Water);
            Assert.AreEqual(0x6890F0, water.PrimaryRgb);
            Assert.AreEqual(0xFFFFFF, water.SecondaryRgb);
        }

        // ---------- 잘못된 데이터: 위치(프로필·필드)를 메시지에 담는다 ----------

        [TestCase("'lungePx': 65", "lungePx")]       // 범위 밖
        [TestCase("'windupSec': '0.5'", "windupSec")] // 종류가 다름
        [TestCase("'mouth': { 'x': -0.6, 'y': 0 }", "mouth")]   // 스프라이트 밖
        public void Profile_BadValue_NamesProfileAndField(string field, string fieldName)
        {
            FailsWith(Json("{ 'pikachu': { " + field + " } }"), "pikachu", fieldName);
        }

        [Test]
        public void UnknownField_IsRejected()
        {
            FailsWith(Json("{ 'pikachu': { 'lunge': 6 } }"), "pikachu", "lunge");
        }

        [Test]
        public void Defaults_MissingField_IsRejected()
        {
            FailsWith(Json(defaults: "{ 'windupSec': 0.15, 'lungePx': 10, 'hitShakePx': 2 }"), "defaults", "knockbackPx");
        }

        [Test]
        public void Type_MissingOrBadColor_NamesTheType()
        {
            FailsWith(Json(types: TypesJson(skip: "fairy")), "fairy");
            FailsWith(Json(types: TypesJson(waterColor: "'#6890F'")), "water", "primary");
        }

        [Test]
        public void MoveFx_BadEffectId_NamesProfileAndMove()
        {
            FailsWith(Json("{ 'pikachu': { 'moveFx': { 'thunderbolt': 'Lightning' } } }"), "pikachu", "thunderbolt");
        }

        // ---------- 실제 데이터 파일 ----------

        static AnimationCatalog RealCatalog() => AnimationCatalog.Parse(ContentDataFilesTests.Read("Data", "animation.json"));

        [Test]
        public void RealFile_EverySpeciesAnimProfile_IsDefined()
        {
            var c = RealCatalog();
            var missing = ContentLoader.ParseSpecies(ContentDataFilesTests.Read("Data", "species.json"))
                .Select(s => s.AnimProfile).Where(k => !c.HasProfile(k)).ToList();
            CollectionAssert.IsEmpty(missing, "animation.json profiles is missing: " + string.Join(", ", missing));
        }

        [Test]
        public void RealFile_MoveFxAndMoveSfxMoveIds_ExistInMoves()
        {
            // 오타가 나면 오버라이드가 조용히 무시되고 기본 연출·소리가 나온다
            var c = RealCatalog();
            var moveIds = new HashSet<string>(ContentLoader.ParseMoves(ContentDataFilesTests.Read("Data", "moves.json")).Select(m => m.Move.Id));
            foreach (var key in c.ProfileKeys)
                foreach (var moveId in c.ProfileFor(key).MoveFx.Keys)
                    Assert.IsTrue(moveIds.Contains(moveId), $"profile '{key}': moveFx '{moveId}' not in moves.json");
            foreach (var moveId in c.MoveSfx.Keys)
                Assert.IsTrue(moveIds.Contains(moveId), $"moveSfx '{moveId}' not in moves.json");
        }
    }
}
