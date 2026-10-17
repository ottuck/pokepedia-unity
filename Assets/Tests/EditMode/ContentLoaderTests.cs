using System;
using System.Linq;
using NUnit.Framework;
using Pokepedia.Content;
using Pokepedia.Domain;

namespace Pokepedia.Tests
{
    /// <summary>문자열 픽스처로 ContentLoader 의 변환·검증을 확인한다. 실제 데이터 파일은 ContentDataFilesTests 가 본다.</summary>
    public class ContentLoaderTests
    {
        // JSON 안의 큰따옴표를 ' 로 적고 Q() 로 바꿔 읽기 쉽게 한다.
        static string Q(string s) => s.Replace('\'', '"');

        static string SpeciesJson(int id, string types, string extra = "")
            => Q("{ 'id': " + id + ", 'types': [" + types + "], " +
                 "'baseStats': { 'hp': 35, 'attack': 55, 'defense': 40, 'spAttack': 50, 'spDefense': 50, 'speed': 90 }, " +
                 "'sprites': { 'front': 'front_" + id + "_sheet', 'back': 'back_" + id + "_sheet', 'frontShiny': 'front_" + id + "_shiny_sheet', 'backShiny': 'back_" + id + "_shiny_sheet' }, " +
                 "'animProfile': 'p" + id + "', 'nameKey': 'species." + id.ToString("000") + "', 'legendary': false" + extra + " }");

        static string MoveJson(string id, string type, string role, string category = "special", int power = 36, int accuracy = 95, string animKind = "projectile")
            => Q("{ 'id': '" + id + "', 'type': '" + type + "', 'category': '" + category + "', 'power': " + power +
                 ", 'accuracy': " + accuracy + ", 'nameKey': 'move." + id + "', 'animKind': '" + animKind + "', 'role': '" + role + "' }");

        static readonly string Moves = "[" + string.Join(",",
            MoveJson("tackle", "normal", "basic", "physical", 16, 100, "contact"),
            MoveJson("thunderbolt", "electric", "signature"),
            MoveJson("thunder", "electric", "secondary", power: 45, accuracy: 70, animKind: "effect"),
            MoveJson("flamethrower", "fire", "signature"),
            MoveJson("ember", "fire", "secondary"),
            MoveJson("wing_attack", "flying", "signature", "physical", animKind: "contact")) + "]";

        static readonly string Species = "[" + string.Join(",",
            SpeciesJson(25, Q("'electric'")),
            SpeciesJson(6, Q("'fire', 'flying'"))) + "]";

        static readonly string Roster = Q("[ { 'id': 25, 'tier': 'showcase', 'npcWeight': 10 }, { 'id': 6, 'tier': 'standard', 'npcWeight': 3 } ]");

        static FormatException Fails(TestDelegate act) => Assert.Throws<FormatException>(act);

        // ---------- 변환 ----------

        [Test]
        public void Species_IsConvertedToDomainAndDisplayInfo()
        {
            var all = ContentLoader.ParseSpecies(Species);
            var s = all[0];
            Assert.AreEqual(25, s.Species.Id);
            Assert.AreEqual(new StatBlock(35, 55, 40, 50, 50, 90), s.Species.BaseStats);
            Assert.AreEqual(new SpriteSet("front_25_sheet", "back_25_sheet", "front_25_shiny_sheet", "back_25_shiny_sheet"), s.Sprites);
            Assert.AreEqual("p25", s.AnimProfile);
            Assert.AreEqual("species.025", s.NameKey);
            CollectionAssert.AreEqual(new[] { PokemonType.Fire, PokemonType.Flying }, all[1].Species.Types, "dual types keep order");
        }

        [Test]
        public void Move_IsConvertedToDomainAndDisplayInfo()
        {
            var tackle = ContentLoader.ParseMoves(Moves)[0];
            Assert.AreEqual(new Move("tackle", PokemonType.Normal, MoveCategory.Physical, 16, 100), tackle.Move);
            Assert.AreEqual("move.tackle", tackle.NameKey);
            Assert.AreEqual(AnimKind.Contact, tackle.AnimKind);
            Assert.AreEqual(MoveRole.Basic, tackle.Role);
        }

        [Test]
        public void Roster_IsParsed()
        {
            var r = ContentLoader.ParseRoster(Roster);
            Assert.AreEqual(new RosterEntry(25, RosterTier.Showcase, 10), r[0]);
            Assert.AreEqual(new RosterEntry(6, RosterTier.Standard, 3), r[1]);
        }

        [Test]
        public void MoveSet_IsBasicPlusTwoTypeMoves()
        {
            // 기술은 타입에서 자동 배정 (PROJECT.md 6.3): 기본기 + 속성 기술 2개.
            // 단일 타입은 그 타입의 대표 기술과 보조 기술, 이중 타입은 타입마다 대표 기술 하나씩 (보조 기술은 받지 않는다).
            var c = ContentLoader.Load(Species, Moves, Roster);
            CollectionAssert.AreEqual(new[] { "tackle", "thunderbolt", "thunder" }, c.MoveSetFor(25).Select(m => m.Move.Id));
            CollectionAssert.AreEqual(new[] { "tackle", "flamethrower", "wing_attack" }, c.MoveSetFor(6).Select(m => m.Move.Id));
        }

        // ---------- 잘못된 데이터 ----------

        [Test]
        public void Error_NamesTheEntryAndValue()
        {
            var ex = Fails(() => ContentLoader.ParseSpecies("[" + SpeciesJson(6, Q("'fire', 'fyre'")) + "]"));
            StringAssert.Contains("6", ex.Message);
            StringAssert.Contains("fyre", ex.Message);
        }

        [Test]
        public void MissingField_IsRejected()
        {
            var json = "[" + SpeciesJson(25, Q("'electric'")).Replace(Q(", 'legendary': false"), "") + "]";
            StringAssert.Contains("legendary", Fails(() => ContentLoader.ParseSpecies(json)).Message);
        }

        [Test]
        public void UnknownField_IsRejected()
        {
            // 기술은 타입에서 자동 생성하므로 species 에 moves 를 적으면 오타·오해로 보고 거부한다.
            var ex = Fails(() => ContentLoader.ParseSpecies("[" + SpeciesJson(25, Q("'electric'"), Q(", 'moves': ['tackle']")) + "]"));
            StringAssert.Contains("moves", ex.Message);
        }

        [TestCase("")]
        [TestCase("'fire', 'flying', 'water'")]
        [TestCase("'fire', 'fire'")]
        public void Species_TypeCountMustBeOneOrTwoDistinct(string types)
        {
            Fails(() => ContentLoader.ParseSpecies("[" + SpeciesJson(6, Q(types)) + "]"));
        }

        [TestCase(0, 95)]
        [TestCase(36, 101)]
        public void Move_PowerAndAccuracyRanges(int power, int accuracy)
        {
            Fails(() => ContentLoader.ParseMoves("[" + MoveJson("zap", "electric", "signature", power: power, accuracy: accuracy) + "]"));
        }

        [Test]
        public void Roster_NegativeWeight_IsRejected()
        {
            Fails(() => ContentLoader.ParseRoster(Q("[ { 'id': 25, 'tier': 'standard', 'npcWeight': -1 } ]")));
        }

        [Test]
        public void DuplicateIds_AreRejected()
        {
            Fails(() => ContentLoader.ParseSpecies("[" + SpeciesJson(25, Q("'electric'")) + "," + SpeciesJson(25, Q("'electric'")) + "]"));
            Fails(() => ContentLoader.ParseMoves("[" + MoveJson("zap", "electric", "signature") + "," + MoveJson("zap", "water", "signature") + "]"));
            Fails(() => ContentLoader.ParseRoster(Q("[ { 'id': 25, 'tier': 'standard', 'npcWeight': 1 }, { 'id': 25, 'tier': 'showcase', 'npcWeight': 1 } ]")));
        }

        // ---------- 교차 검증 ----------

        [Test]
        public void SpeciesTypeWithoutSignatureMove_IsRejected()
        {
            var ex = Fails(() => ContentLoader.Load("[" + SpeciesJson(9, Q("'water'")) + "]", Moves, "[]"));
            StringAssert.Contains("water", ex.Message);
        }

        [Test]
        public void RosterIdMissingFromSpecies_IsRejected()
        {
            var roster = Q("[ { 'id': 150, 'tier': 'standard', 'npcWeight': 1 } ]");
            StringAssert.Contains("150", Fails(() => ContentLoader.Load(Species, Moves, roster)).Message);
        }

        [Test]
        public void ExactlyOneBasicMove_AndAtMostOneSignatureAndOneSecondaryPerType()
        {
            var none = "[" + MoveJson("thunderbolt", "electric", "signature") + "]";
            var two = "[" + MoveJson("tackle", "normal", "basic") + "," + MoveJson("pound", "normal", "basic") + "]";
            var twoSig = "[" + MoveJson("tackle", "normal", "basic") + "," + MoveJson("thunderbolt", "electric", "signature") + "," + MoveJson("spark", "electric", "signature") + "]";
            Fails(() => ContentLoader.Load("[]", none, "[]"));
            Fails(() => ContentLoader.Load("[]", two, "[]"));
            Fails(() => ContentLoader.Load("[]", twoSig, "[]"));
            var twoSecondary = "[" + MoveJson("tackle", "normal", "basic") + "," + MoveJson("thunderbolt", "electric", "signature") + "," +
                               MoveJson("thunder", "electric", "secondary") + "," + MoveJson("zap_cannon", "electric", "secondary") + "]";
            Fails(() => ContentLoader.Load("[]", twoSecondary, "[]"));
        }
    }
}
