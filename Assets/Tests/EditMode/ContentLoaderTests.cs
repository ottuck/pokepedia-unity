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

        static string SpeciesJson(int id, string types, string extra = "", string stats = null)
            => Q("{ 'id': " + id + ", 'types': [" + types + "], " +
                 "'baseStats': " + (stats ?? "{ 'hp': 35, 'attack': 55, 'defense': 40, 'spAttack': 50, 'spDefense': 50, 'speed': 90 }") + ", " +
                 "'sprites': { 'front': 'front_" + id + "_sheet', 'back': 'back_" + id + "_sheet', 'frontShiny': 'front_" + id + "_shiny_sheet', 'backShiny': 'back_" + id + "_shiny_sheet' }, " +
                 "'animProfile': 'p" + id + "', 'nameKey': 'species." + id.ToString("000") + "', 'legendary': false" + extra + " }");

        static string MoveJson(string id, string type, string role, string category = "special", int power = 36, int accuracy = 95, string animKind = "projectile")
            => Q("{ 'id': '" + id + "', 'type': '" + type + "', 'category': '" + category + "', 'power': " + power +
                 ", 'accuracy': " + accuracy + ", 'nameKey': 'move." + id + "', 'animKind': '" + animKind + "', 'role': '" + role + "' }");

        static readonly string Moves = "[" + string.Join(",",
            MoveJson("tackle", "normal", "basic", "physical", 16, 100, "contact"),
            MoveJson("thunderbolt", "electric", "signature"),
            MoveJson("flamethrower", "fire", "signature"),
            MoveJson("wing_attack", "flying", "signature", "physical", animKind: "contact")) + "]";

        static readonly string Species = "[" + string.Join(",",
            SpeciesJson(25, Q("'electric'")),
            SpeciesJson(6, Q("'fire', 'flying'"))) + "]";

        static readonly string Roster = Q("[ { 'id': 25, 'tier': 'showcase', 'npcWeight': 10 }, { 'id': 6, 'tier': 'standard', 'npcWeight': 3 } ]");

        static FormatException Fails(TestDelegate act) => Assert.Throws<FormatException>(act);

        // ---------- species ----------

        [Test]
        public void Species_IsConvertedToDomainAndDisplayInfo()
        {
            var s = ContentLoader.ParseSpecies(Species)[0];
            Assert.AreEqual(25, s.Species.Id);
            CollectionAssert.AreEqual(new[] { PokemonType.Electric }, s.Species.Types);
            Assert.AreEqual(new StatBlock(35, 55, 40, 50, 50, 90), s.Species.BaseStats);
            Assert.AreEqual(new SpriteSet("front_25_sheet", "back_25_sheet", "front_25_shiny_sheet", "back_25_shiny_sheet"), s.Sprites);
            Assert.AreEqual("p25", s.AnimProfile);
            Assert.AreEqual("species.025", s.NameKey);
            Assert.IsFalse(s.Legendary);
        }

        [Test]
        public void Species_DualTypes_KeepOrder()
        {
            var s = ContentLoader.ParseSpecies(Species)[1];
            CollectionAssert.AreEqual(new[] { PokemonType.Fire, PokemonType.Flying }, s.Species.Types);
        }

        [Test]
        public void Species_UnknownType_NamesTheEntry()
        {
            var ex = Fails(() => ContentLoader.ParseSpecies("[" + SpeciesJson(6, Q("'fire', 'fyre'")) + "]"));
            StringAssert.Contains("6", ex.Message);
            StringAssert.Contains("fyre", ex.Message);
        }

        [Test]
        public void Species_TypeNamesAreLowercaseOnly()
        {
            Fails(() => ContentLoader.ParseSpecies("[" + SpeciesJson(25, Q("'Electric'")) + "]"));
        }

        [Test]
        public void Species_MissingStat_NamesEntryAndField()
        {
            var json = "[" + SpeciesJson(9, Q("'water'"), stats: Q("{ 'hp': 79, 'attack': 83, 'defense': 100, 'spAttack': 85, 'spDefense': 105 }")) + "]";
            var ex = Fails(() => ContentLoader.ParseSpecies(json));
            StringAssert.Contains("9", ex.Message);
            StringAssert.Contains("speed", ex.Message);
        }

        [Test]
        public void Species_MissingTopLevelField_NamesIt()
        {
            var json = "[" + SpeciesJson(25, Q("'electric'")).Replace(Q(", 'legendary': false"), "") + "]";
            var ex = Fails(() => ContentLoader.ParseSpecies(json));
            StringAssert.Contains("legendary", ex.Message);
        }

        [Test]
        public void Species_FractionalStat_IsRejected()
        {
            var json = "[" + SpeciesJson(25, Q("'electric'"), stats: Q("{ 'hp': 35.5, 'attack': 55, 'defense': 40, 'spAttack': 50, 'spDefense': 50, 'speed': 90 }")) + "]";
            StringAssert.Contains("hp", Fails(() => ContentLoader.ParseSpecies(json)).Message);
        }

        [Test]
        public void Species_WrongValueKind_IsRejected()
        {
            Fails(() => ContentLoader.ParseSpecies("[" + SpeciesJson(25, Q("'electric'")).Replace("\"legendary\": false", "\"legendary\": \"no\"") + "]"));
        }

        [TestCase("")]
        [TestCase("'fire', 'flying', 'water'")]
        [TestCase("'fire', 'fire'")]
        public void Species_TypeCountMustBeOneOrTwoDistinct(string types)
        {
            Fails(() => ContentLoader.ParseSpecies("[" + SpeciesJson(6, Q(types)) + "]"));
        }

        [Test]
        public void Species_UnknownField_IsRejected()
        {
            // 기술은 타입에서 자동 생성하므로(PROJECT.md 6.3) species 에 moves 를 적으면 오타·오해로 보고 거부한다.
            var ex = Fails(() => ContentLoader.ParseSpecies("[" + SpeciesJson(25, Q("'electric'"), Q(", 'moves': ['tackle']")) + "]"));
            StringAssert.Contains("moves", ex.Message);
        }

        [Test]
        public void Species_DuplicateId_IsRejected()
        {
            var json = "[" + SpeciesJson(25, Q("'electric'")) + "," + SpeciesJson(25, Q("'electric'")) + "]";
            StringAssert.Contains("25", Fails(() => ContentLoader.ParseSpecies(json)).Message);
        }

        [Test]
        public void TopLevel_MustBeArray()
        {
            Fails(() => ContentLoader.ParseSpecies("{}"));
            Fails(() => ContentLoader.ParseMoves("{}"));
            Fails(() => ContentLoader.ParseRoster("{}"));
        }

        // ---------- moves ----------

        [Test]
        public void Move_IsConvertedToDomainAndDisplayInfo()
        {
            var moves = ContentLoader.ParseMoves(Moves);
            var tackle = moves[0];
            Assert.AreEqual(new Move("tackle", PokemonType.Normal, MoveCategory.Physical, 16, 100), tackle.Move);
            Assert.AreEqual("move.tackle", tackle.NameKey);
            Assert.AreEqual(AnimKind.Contact, tackle.AnimKind);
            Assert.AreEqual(MoveRole.Basic, tackle.Role);

            var bolt = moves[1];
            Assert.AreEqual(new Move("thunderbolt", PokemonType.Electric, MoveCategory.Special, 36, 95), bolt.Move);
            Assert.AreEqual(AnimKind.Projectile, bolt.AnimKind);
            Assert.AreEqual(MoveRole.Signature, bolt.Role);
        }

        [TestCase("category", "magic")]
        [TestCase("animKind", "dance")]
        [TestCase("role", "ultimate")]
        [TestCase("type", "sound")]
        public void Move_UnknownEnumValue_NamesMove(string field, string value)
        {
            var json = Q("[{ 'id': 'zap', 'type': 'electric', 'category': 'special', 'power': 36, 'accuracy': 95, 'nameKey': 'move.zap', 'animKind': 'effect', 'role': 'signature' }]");
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"" + field + "\": \"[a-z]+\"", "\"" + field + "\": \"" + value + "\"");
            var ex = Fails(() => ContentLoader.ParseMoves(json));
            StringAssert.Contains("zap", ex.Message);
            StringAssert.Contains(value, ex.Message);
        }

        [TestCase(0, 95)]
        [TestCase(-5, 95)]
        [TestCase(36, 0)]
        [TestCase(36, 101)]
        public void Move_PowerAndAccuracyRanges(int power, int accuracy)
        {
            Fails(() => ContentLoader.ParseMoves("[" + MoveJson("zap", "electric", "signature", power: power, accuracy: accuracy) + "]"));
        }

        [TestCase("Tackle")]
        [TestCase("hydro-pump")]
        [TestCase("")]
        public void Move_IdMustBeSnakeCase(string id)
        {
            Fails(() => ContentLoader.ParseMoves("[" + MoveJson(id, "normal", "basic") + "]"));
        }

        [Test]
        public void Move_DuplicateId_IsRejected()
        {
            var json = "[" + MoveJson("zap", "electric", "signature") + "," + MoveJson("zap", "water", "signature") + "]";
            StringAssert.Contains("zap", Fails(() => ContentLoader.ParseMoves(json)).Message);
        }

        // ---------- roster ----------

        [Test]
        public void Roster_IsParsed()
        {
            var r = ContentLoader.ParseRoster(Roster);
            Assert.AreEqual(2, r.Count);
            Assert.AreEqual(new RosterEntry(25, RosterTier.Showcase, 10), r[0]);
            Assert.AreEqual(new RosterEntry(6, RosterTier.Standard, 3), r[1]);
        }

        [Test]
        public void Roster_UnknownTier_IsRejected()
        {
            StringAssert.Contains("gold", Fails(() => ContentLoader.ParseRoster(Q("[ { 'id': 25, 'tier': 'gold', 'npcWeight': 1 } ]"))).Message);
        }

        [Test]
        public void Roster_NegativeWeight_IsRejected()
        {
            Fails(() => ContentLoader.ParseRoster(Q("[ { 'id': 25, 'tier': 'standard', 'npcWeight': -1 } ]")));
        }

        [Test]
        public void Roster_DuplicateId_IsRejected()
        {
            Fails(() => ContentLoader.ParseRoster(Q("[ { 'id': 25, 'tier': 'standard', 'npcWeight': 1 }, { 'id': 25, 'tier': 'showcase', 'npcWeight': 1 } ]")));
        }

        // ---------- catalog: 교차 검증과 기술 자동 배정 ----------

        [Test]
        public void MoveSet_IsBasicThenOneSignaturePerType()
        {
            var c = ContentLoader.Load(Species, Moves, Roster);
            CollectionAssert.AreEqual(new[] { "tackle", "thunderbolt" }, c.MoveSetFor(25).Select(m => m.Move.Id));
            CollectionAssert.AreEqual(new[] { "tackle", "flamethrower", "wing_attack" }, c.MoveSetFor(6).Select(m => m.Move.Id));
        }

        [Test]
        public void MovesFor_ReturnsDomainMoves()
        {
            var c = ContentLoader.Load(Species, Moves, Roster);
            var moves = c.MovesFor(25);
            Assert.AreEqual(new Move("thunderbolt", PokemonType.Electric, MoveCategory.Special, 36, 95), moves[1]);
        }

        [Test]
        public void Catalog_Lookups()
        {
            var c = ContentLoader.Load(Species, Moves, Roster);
            Assert.AreEqual(6, c.GetSpecies(6).Species.Id);
            Assert.AreEqual("tackle", c.BasicMove.Move.Id);
            Assert.AreEqual("wing_attack", c.GetMove("wing_attack").Move.Id);
            Assert.AreEqual("flamethrower", c.SignatureMoveFor(PokemonType.Fire).Move.Id);
            Assert.IsNull(c.SignatureMoveFor(PokemonType.Ice));
            Assert.AreEqual(2, c.Roster.Count);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => c.GetSpecies(999));
        }

        [Test]
        public void SpeciesTypeWithoutSignatureMove_NamesSpeciesAndType()
        {
            var species = "[" + SpeciesJson(9, Q("'water'")) + "]";
            var ex = Fails(() => ContentLoader.Load(species, Moves, "[]"));
            StringAssert.Contains("9", ex.Message);
            StringAssert.Contains("water", ex.Message);
        }

        [Test]
        public void RosterIdMissingFromSpecies_IsRejected()
        {
            var roster = Q("[ { 'id': 150, 'tier': 'standard', 'npcWeight': 1 } ]");
            StringAssert.Contains("150", Fails(() => ContentLoader.Load(Species, Moves, roster)).Message);
        }

        [Test]
        public void ExactlyOneBasicMove_IsRequired()
        {
            var none = "[" + MoveJson("thunderbolt", "electric", "signature") + "]";
            var two = "[" + MoveJson("tackle", "normal", "basic") + "," + MoveJson("pound", "normal", "basic") + "]";
            Fails(() => ContentLoader.Load("[]", none, "[]"));
            Fails(() => ContentLoader.Load("[]", two, "[]"));
        }

        [Test]
        public void TwoSignatureMovesForOneType_AreRejected()
        {
            var moves = "[" + MoveJson("tackle", "normal", "basic") + "," + MoveJson("thunderbolt", "electric", "signature") + "," + MoveJson("spark", "electric", "signature") + "]";
            StringAssert.Contains("electric", Fails(() => ContentLoader.Load("[]", moves, "[]")).Message);
        }
    }
}
