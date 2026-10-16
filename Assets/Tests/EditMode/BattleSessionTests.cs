using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pokepedia.App.Battle;
using Pokepedia.Content;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    /// <summary>
    /// 배틀 흐름(PROJECT.md 5.3, 7.2 Application). 문자열 픽스처로 만든 작은 카탈로그를 쓴다:
    /// 25 = 전기·강함(종족값 200), 9·7 = 물·약함(종족값 10), 26 = 전기·보통(80), 8 = 물·보통(80), 150 = 가중치 0.
    /// 모든 기술은 명중 100 이라 빗나감 난수를 쓰지 않는다.
    /// </summary>
    public class BattleSessionTests
    {
        static string Q(string s) => s.Replace('\'', '"');

        static string SpeciesJson(int id, string type, int stat)
            => Q("{ 'id': " + id + ", 'types': ['" + type + "'], " +
                 "'baseStats': { 'hp': " + stat + ", 'attack': " + stat + ", 'defense': " + stat + ", 'spAttack': " + stat + ", 'spDefense': " + stat + ", 'speed': " + stat + " }, " +
                 "'sprites': { 'front': 'front_" + id + "_sheet', 'back': 'back_" + id + "_sheet', 'frontShiny': 'front_" + id + "_shiny_sheet', 'backShiny': 'back_" + id + "_shiny_sheet' }, " +
                 "'animProfile': 'p" + id + "', 'nameKey': 'species." + id.ToString("000") + "', 'legendary': false }");

        static string MoveJson(string id, string type, string role, int power)
            => Q("{ 'id': '" + id + "', 'type': '" + type + "', 'category': 'special', 'power': " + power +
                 ", 'accuracy': 100, 'nameKey': 'move." + id + "', 'animKind': 'projectile', 'role': '" + role + "' }");

        internal static ContentCatalog Catalog(string roster = null)
        {
            var species = "[" + string.Join(",",
                SpeciesJson(25, "electric", 200), SpeciesJson(26, "electric", 80),
                SpeciesJson(9, "water", 10), SpeciesJson(7, "water", 10), SpeciesJson(8, "water", 80),
                SpeciesJson(150, "electric", 80)) + "]";
            var moves = "[" + string.Join(",",
                MoveJson("tackle", "normal", "basic", 16),
                MoveJson("thunderbolt", "electric", "signature", 35),
                MoveJson("hydro_pump", "water", "signature", 35)) + "]";
            roster ??= Q("[ { 'id': 25, 'tier': 'showcase', 'npcWeight': 10 }, { 'id': 9, 'tier': 'showcase', 'npcWeight': 10 }, " +
                         "{ 'id': 7, 'tier': 'standard', 'npcWeight': 10 }, { 'id': 150, 'tier': 'standard', 'npcWeight': 0 } ]");
            return ContentLoader.Load(species, moves, roster);
        }

        static readonly BattleRules Rules = BattleRules.Default;

        /// <summary>정해 둔 순서대로 상대 id 를 돌려주고 호출 횟수를 센다.</summary>
        sealed class Picks
        {
            readonly Queue<int> ids;
            public int Calls { get; private set; }
            public Picks(params int[] ids) => this.ids = new Queue<int>(ids);
            public int Next() { Calls++; return ids.Dequeue(); }
        }

        static BattleSession Session(int player, Picks picks, BattleMode mode = BattleMode.Collection, uint seed = 1234)
            => new BattleSession(Catalog(), Rules, new SeededRandom(seed), mode, player, picks.Next);

        /// <summary>한쪽이 이길 때까지 플레이어가 moveIndex 만 쓴다. 모든 이벤트를 돌려준다.</summary>
        static List<BattleEvent> PlayOut(BattleSession s, int moveIndex)
        {
            var all = new List<BattleEvent>();
            for (int i = 0; !s.State.IsOver; i++)
            {
                Assert.That(i, Is.LessThan(100), "battle did not end");
                all.AddRange(s.UseMove(moveIndex));
            }
            return all;
        }

        // ---------- 생성·시작 ----------

        [Test]
        public void Constructor_UsesPlayerSpeciesAndPickedOpponent()
        {
            var picks = new Picks(9);
            var s = Session(25, picks);
            Assert.AreEqual(1, picks.Calls);
            Assert.AreEqual(BattleMode.Collection, s.Mode);
            Assert.AreEqual(25, s.Player.Species.Id);
            Assert.AreEqual(9, s.Npc.Species.Id);
            Assert.AreSame(s.Player, s.State.Sides[BattleState.Player].Active);
            Assert.AreSame(s.Npc, s.State.Sides[BattleState.Npc].Active);
            Assert.AreEqual(s.Player.MaxHp, s.Player.Hp);
            Assert.AreEqual(s.Npc.MaxHp, s.Npc.Hp);
        }

        [Test]
        public void Battlers_AreBuiltFromCatalogMovesAndRules()
        {
            var c = Catalog();
            var s = new BattleSession(c, Rules, new SeededRandom(1), BattleMode.Demo, 25, () => 9);
            var expected = Battler.Create(c.GetSpecies(25).Species, c.MovesFor(25), Rules);
            Assert.AreEqual(expected.Stats, s.Player.Stats);
            CollectionAssert.AreEqual(c.MovesFor(25), s.Player.Moves);
            CollectionAssert.AreEqual(c.MovesFor(9), s.Npc.Moves);
        }

        [Test]
        public void PlayerMoveInfos_MatchPlayerMovesByIndex()
        {
            var s = Session(25, new Picks(9));
            CollectionAssert.AreEqual(new[] { "tackle", "thunderbolt" }, s.PlayerMoveInfos.Select(m => m.Id));
            CollectionAssert.AreEqual(s.Player.Moves, s.PlayerMoveInfos.Select(m => m.Move));
            Assert.AreEqual("move.thunderbolt", s.PlayerMoveInfos[1].NameKey);
        }

        [Test]
        public void Start_ReturnsNpcThenPlayerSentOut()
        {
            var s = Session(25, new Picks(9));
            var events = s.Start();
            CollectionAssert.AreEqual(new BattleEvent[]
            {
                new PokemonSentOut(BattleState.Npc, 9, s.Npc.MaxHp, s.Npc.MaxHp),
                new PokemonSentOut(BattleState.Player, 25, s.Player.MaxHp, s.Player.MaxHp),
            }, events);
        }

        [Test]
        public void Constructor_RejectsMissingArguments()
        {
            var c = Catalog();
            Assert.Throws<ArgumentNullException>(() => new BattleSession(null, Rules, new SeededRandom(1), BattleMode.Demo, 25, () => 9));
            Assert.Throws<ArgumentNullException>(() => new BattleSession(c, null, new SeededRandom(1), BattleMode.Demo, 25, () => 9));
            Assert.Throws<ArgumentNullException>(() => new BattleSession(c, Rules, null, BattleMode.Demo, 25, () => 9));
            Assert.Throws<ArgumentNullException>(() => new BattleSession(c, Rules, new SeededRandom(1), BattleMode.Demo, 25, null));
            Assert.Throws<KeyNotFoundException>(() => new BattleSession(c, Rules, new SeededRandom(1), BattleMode.Demo, 999, () => 9));
            Assert.Throws<KeyNotFoundException>(() => new BattleSession(c, Rules, new SeededRandom(1), BattleMode.Demo, 25, () => 999));
        }

        // ---------- 턴 ----------

        [Test]
        public void UseMove_PlaysOneTurnWithNpcAiChoice_SameAsEngineDirectly()
        {
            // 보통 vs 보통(스피드 같음 → 순서 난수 사용). 세션의 결과가 같은 시드의 엔진을 직접 돌린 결과와 같아야 한다.
            var c = Catalog();
            const uint seed = 77;
            var s = new BattleSession(c, Rules, new SeededRandom(seed), BattleMode.Collection, 26, () => 8);
            s.Start();
            var actual = s.UseMove(0);

            var p = Battler.Create(c.GetSpecies(26).Species, c.MovesFor(26), Rules);
            var n = Battler.Create(c.GetSpecies(8).Species, c.MovesFor(8), Rules);
            var engine = new BattleEngine(new BattleState(new BattleSide(p), new BattleSide(n)), Rules, new SeededRandom(seed));
            engine.Start();
            var npcChoice = NpcBattleAi.Choose(n, p, Rules);
            Assert.AreEqual(new UseMove(1), npcChoice, "fixture: water NPC prefers hydro_pump");
            var expected = engine.PlayTurn(new UseMove(0), npcChoice);

            CollectionAssert.AreEqual(expected, actual);
            CollectionAssert.Contains(actual, new MoveUsed(BattleState.Player, "tackle"));
            CollectionAssert.Contains(actual, new MoveUsed(BattleState.Npc, "hydro_pump"));
            Assert.AreEqual(1, s.State.Turn);
        }

        [Test]
        public void UseMove_BeforeStart_Throws()
        {
            var s = Session(25, new Picks(9));
            Assert.Throws<InvalidOperationException>(() => s.UseMove(0));
        }

        [Test]
        public void UseMove_InvalidIndex_Throws()
        {
            var s = Session(25, new Picks(9));
            s.Start();
            Assert.Throws<ArgumentOutOfRangeException>(() => s.UseMove(5));
        }

        [Test]
        public void StrongPlayer_Wins_ThenUseMoveThrows()
        {
            var s = Session(25, new Picks(9));
            s.Start();
            var events = PlayOut(s, 1);
            Assert.AreEqual(BattleState.Player, s.State.Winner);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(new BattleEnded(BattleState.Player), events.Last());
            CollectionAssert.Contains(events, new Fainted(BattleState.Npc));
            Assert.Throws<InvalidOperationException>(() => s.UseMove(0));
        }

        [Test]
        public void WeakPlayer_Loses()
        {
            var s = Session(9, new Picks(25));
            s.Start();
            var events = PlayOut(s, 0);
            Assert.AreEqual(BattleState.Npc, s.State.Winner);
            Assert.AreEqual(new BattleEnded(BattleState.Npc), events.Last());
            Assert.AreEqual(0, s.Player.Hp);
        }

        // ---------- 승리 후 다음 상대 / 패배 후 다시 도전 ----------

        [Test]
        public void NextBattle_AfterWin_PicksNewOpponent_FullHp_NewEngine()
        {
            var picks = new Picks(9, 7);
            var s = Session(25, picks);
            s.Start();
            PlayOut(s, 1);
            var oldState = s.State;

            var events = s.NextBattle();

            Assert.AreEqual(2, picks.Calls);
            Assert.AreNotSame(oldState, s.State);
            Assert.AreEqual(7, s.Npc.Species.Id);
            Assert.AreEqual(25, s.Player.Species.Id);
            Assert.AreEqual(s.Player.MaxHp, s.Player.Hp);
            Assert.IsFalse(s.State.IsOver);
            Assert.AreEqual(0, s.State.Turn);
            CollectionAssert.AreEqual(new BattleEvent[]
            {
                new PokemonSentOut(BattleState.Npc, 7, s.Npc.MaxHp, s.Npc.MaxHp),
                new PokemonSentOut(BattleState.Player, 25, s.Player.MaxHp, s.Player.MaxHp),
            }, events);
            Assert.DoesNotThrow(() => s.UseMove(0));
        }

        [Test]
        public void NextBattle_RestoresPlayerHpTakenInPreviousBattle()
        {
            // 보통 vs 보통으로 몇 턴 진행해 HP 를 깎은 뒤 다음 상대로 넘어간다.
            var c = Catalog();
            var picks = new Picks(8, 9);
            var s = new BattleSession(c, Rules, new SeededRandom(5), BattleMode.Collection, 26, picks.Next);
            s.Start();
            s.UseMove(0);
            Assert.That(s.Player.Hp, Is.LessThan(s.Player.MaxHp), "fixture: player took damage");
            s.NextBattle();
            Assert.AreEqual(s.Player.MaxHp, s.Player.Hp);
            Assert.AreEqual(9, s.Npc.Species.Id);
        }

        [Test]
        public void Retry_AfterLoss_SameOpponent_FullHp_NewEngine()
        {
            var picks = new Picks(25);
            var s = Session(9, picks);
            s.Start();
            PlayOut(s, 0);
            var oldNpc = s.Npc;

            var events = s.Retry();

            Assert.AreEqual(1, picks.Calls, "retry must not pick a new opponent");
            Assert.AreEqual(25, s.Npc.Species.Id);
            Assert.AreNotSame(oldNpc, s.Npc);
            Assert.AreEqual(s.Player.MaxHp, s.Player.Hp);
            Assert.AreEqual(s.Npc.MaxHp, s.Npc.Hp);
            Assert.IsFalse(s.State.IsOver);
            Assert.AreEqual(0, s.State.Turn);
            CollectionAssert.AreEqual(new BattleEvent[]
            {
                new PokemonSentOut(BattleState.Npc, 25, s.Npc.MaxHp, s.Npc.MaxHp),
                new PokemonSentOut(BattleState.Player, 9, s.Player.MaxHp, s.Player.MaxHp),
            }, events);
            Assert.DoesNotThrow(() => s.UseMove(0));
        }

        // ---------- 도망 ----------

        [Test]
        public void TryRun_Demo_CannotEscape_AndBattleContinuesUnchanged()
        {
            var c = Catalog();
            var s = new BattleSession(c, Rules, new SeededRandom(3), BattleMode.Demo, 26, () => 8);
            s.Start();
            s.UseMove(0);
            var state = s.State;
            var player = s.Player;
            var npc = s.Npc;
            int playerHp = player.Hp, npcHp = npc.Hp, turn = state.Turn;

            Assert.AreEqual(RunOutcome.CannotEscape, s.TryRun());

            Assert.AreSame(state, s.State);
            Assert.AreSame(player, s.Player);
            Assert.AreSame(npc, s.Npc);
            Assert.AreEqual(playerHp, s.Player.Hp);
            Assert.AreEqual(npcHp, s.Npc.Hp);
            Assert.AreEqual(turn, s.State.Turn);
            Assert.DoesNotThrow(() => s.UseMove(0));
        }

        [Test]
        public void TryRun_Collection_Escapes_ThenNextBattleBringsNewOpponent()
        {
            var picks = new Picks(9, 7);
            var s = Session(25, picks);
            s.Start();

            Assert.IsFalse(s.IsOver);
            Assert.AreEqual(RunOutcome.Escaped, s.TryRun());
            Assert.AreEqual(1, picks.Calls, "escape itself does not pick; the caller calls NextBattle()");
            Assert.IsTrue(s.IsOver, "an escaped battle is over (no winner)");
            Assert.IsNull(s.State.Winner);
            Assert.Throws<InvalidOperationException>(() => s.UseMove(0), "the escaped battle is over");

            s.NextBattle();
            Assert.IsFalse(s.IsOver);
            Assert.AreEqual(7, s.Npc.Species.Id);
            Assert.AreEqual(s.Player.MaxHp, s.Player.Hp);
            Assert.DoesNotThrow(() => s.UseMove(0));
        }

        [Test]
        public void TryRun_AfterBattleEnded_Throws()
        {
            var s = Session(25, new Picks(9));
            s.Start();
            PlayOut(s, 1);
            Assert.Throws<InvalidOperationException>(() => s.TryRun());
        }

        // ---------- 재현성 ----------

        static List<BattleEvent> Script(uint seed)
        {
            var c = Catalog();
            var random = new SeededRandom(seed);
            var s = new BattleSession(c, Rules, random, BattleMode.Collection, 26, OpponentPicker.Weighted(c, random, excludeId: 26));
            var log = new List<BattleEvent>();
            log.AddRange(s.Start());
            for (int round = 0; round < 4; round++)
            {
                for (int i = 0; !s.State.IsOver && i < 100; i++) log.AddRange(s.UseMove(i % 2));
                log.AddRange(s.State.Winner == BattleState.Player ? s.NextBattle() : s.Retry());
            }
            log.AddRange(s.UseMove(0));
            if (!s.State.IsOver && s.TryRun() == RunOutcome.Escaped) log.AddRange(s.NextBattle());
            return log;
        }

        [Test]
        public void SameSeed_ReproducesWholeSession()
        {
            var a = Script(20261016);
            var b = Script(20261016);
            Assert.That(a.Count, Is.GreaterThan(10));
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void DifferentSeed_ChangesSession()
        {
            CollectionAssert.AreNotEqual(Script(1), Script(2));
        }

        // ---------- 상대 가중 추첨 ----------

        [Test]
        public void Weighted_MapsRandomValueToCumulativeWeights()
        {
            var roster = Q("[ { 'id': 25, 'tier': 'showcase', 'npcWeight': 1 }, { 'id': 9, 'tier': 'showcase', 'npcWeight': 3 }, { 'id': 7, 'tier': 'standard', 'npcWeight': 6 } ]");
            var c = Catalog(roster);
            // 누적: 25 → [0,1), 9 → [1,4), 7 → [4,10)
            var random = ScriptedRandom.Of(0, 1, 3, 4, 9);
            var pick = OpponentPicker.Weighted(c, random);
            CollectionAssert.AreEqual(new[] { 25, 9, 9, 7, 7 }, Enumerable.Range(0, 5).Select(_ => pick()));
            Assert.AreEqual(0, random.Remaining);
        }

        [Test]
        public void Weighted_NeverPicksZeroWeightOrExcluded()
        {
            var c = Catalog(); // 150 은 가중치 0
            var pick = OpponentPicker.Weighted(c, new SeededRandom(9), excludeId: 25);
            var seen = Enumerable.Range(0, 2000).Select(_ => pick()).Distinct().OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(new[] { 7, 9 }, seen);
        }

        [Test]
        public void Weighted_DistributionFollowsWeights()
        {
            var roster = Q("[ { 'id': 25, 'tier': 'showcase', 'npcWeight': 1 }, { 'id': 9, 'tier': 'showcase', 'npcWeight': 3 }, { 'id': 7, 'tier': 'standard', 'npcWeight': 6 }, { 'id': 150, 'tier': 'standard', 'npcWeight': 0 } ]");
            var pick = OpponentPicker.Weighted(Catalog(roster), new SeededRandom(20261016));
            const int n = 20000;
            var counts = Enumerable.Range(0, n).Select(_ => pick()).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            Assert.IsFalse(counts.ContainsKey(150));
            Assert.That(counts[25] / (double)n, Is.EqualTo(0.1).Within(0.02));
            Assert.That(counts[9] / (double)n, Is.EqualTo(0.3).Within(0.02));
            Assert.That(counts[7] / (double)n, Is.EqualTo(0.6).Within(0.02));
        }

        [Test]
        public void Weighted_NoCandidates_Throws()
        {
            var roster = Q("[ { 'id': 25, 'tier': 'showcase', 'npcWeight': 5 }, { 'id': 150, 'tier': 'standard', 'npcWeight': 0 } ]");
            Assert.Throws<InvalidOperationException>(() => OpponentPicker.Weighted(Catalog(roster), new SeededRandom(1), excludeId: 25));
            Assert.Throws<ArgumentNullException>(() => OpponentPicker.Weighted(null, new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => OpponentPicker.Weighted(Catalog(), null));
        }
    }
}
