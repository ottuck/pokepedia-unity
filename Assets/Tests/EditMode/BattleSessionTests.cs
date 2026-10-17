using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pokepedia.App.Battle;
using Pokepedia.App.Host;
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

        [Test]
        public void PlayerMoveInfos_MatchPlayerMovesByIndex()
        {
            // 화면의 기술 메뉴 순서 = 엔진의 기술 인덱스
            var s = Session(25, new Picks(9));
            CollectionAssert.AreEqual(new[] { "tackle", "thunderbolt" }, s.PlayerMoveInfos.Select(m => m.Id));
            CollectionAssert.AreEqual(s.Player.Moves, s.PlayerMoveInfos.Select(m => m.Move));
        }

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

            CollectionAssert.AreEqual(engine.PlayTurn(new UseMove(0), npcChoice), actual);
        }

        [Test]
        public void StrongPlayer_Wins_ThenUseMoveThrows()
        {
            var s = Session(25, new Picks(9));
            s.Start();
            var events = PlayOut(s, 1);
            Assert.AreEqual(BattleState.Player, s.State.Winner);
            Assert.AreEqual(new BattleEnded(BattleState.Player), events.Last());
            Assert.Throws<InvalidOperationException>(() => s.UseMove(0));
        }

        [Test]
        public void NextBattle_AfterWin_PicksNewOpponent_FullHp()
        {
            var picks = new Picks(9, 7);
            var s = Session(25, picks);
            s.Start();
            PlayOut(s, 1);

            var events = s.NextBattle();

            Assert.AreEqual(2, picks.Calls);
            Assert.AreEqual(7, s.Npc.Species.Id);
            Assert.AreEqual(s.Player.MaxHp, s.Player.Hp);
            Assert.AreEqual(0, s.State.Turn);
            CollectionAssert.AreEqual(new BattleEvent[]
            {
                new PokemonSentOut(BattleState.Npc, 7, s.Npc.MaxHp, s.Npc.MaxHp),
                new PokemonSentOut(BattleState.Player, 25, s.Player.MaxHp, s.Player.MaxHp),
            }, events);
        }

        [Test]
        public void NextBattle_RestoresPlayerHpTakenInPreviousBattle()
        {
            var picks = new Picks(8, 9);
            var s = new BattleSession(Catalog(), Rules, new SeededRandom(5), BattleMode.Collection, 26, picks.Next);
            s.Start();
            s.UseMove(0);
            Assert.That(s.Player.Hp, Is.LessThan(s.Player.MaxHp), "fixture: player took damage");
            s.TryRun();                                   // 다음 배틀은 이번 배틀이 끝난 뒤에만 부를 수 있다
            s.NextBattle();
            Assert.AreEqual(s.Player.MaxHp, s.Player.Hp);
        }

        [Test]
        public void NextBattleAndRetry_WhileBattleIsOn_Throw()
        {
            // 진행 중에 불리면 싸우던 배틀이 통째로 사라진다 (화면의 중복 입력 등). 끝난 뒤에만 허용한다.
            var s = Session(25, new Picks(9, 7));
            s.Start();
            s.UseMove(0);
            Assert.IsFalse(s.IsOver, "fixture: battle still on");
            Assert.Throws<InvalidOperationException>(() => s.NextBattle());
            Assert.Throws<InvalidOperationException>(() => s.Retry());
        }

        [Test]
        public void Retry_AfterLoss_SameOpponent_FullHp()
        {
            var picks = new Picks(25);
            var s = Session(9, picks);
            s.Start();
            PlayOut(s, 0);
            Assert.AreEqual(BattleState.Npc, s.State.Winner);

            s.Retry();

            Assert.AreEqual(1, picks.Calls, "retry must not pick a new opponent");
            Assert.AreEqual(25, s.Npc.Species.Id);
            Assert.AreEqual(s.Player.MaxHp, s.Player.Hp);
            Assert.AreEqual(s.Npc.MaxHp, s.Npc.Hp);
            Assert.IsFalse(s.State.IsOver);
        }

        [Test]
        public void TryRun_Demo_CannotEscape_AndBattleContinues()
        {
            var s = new BattleSession(Catalog(), Rules, new SeededRandom(3), BattleMode.Demo, 26, () => 8);
            s.Start();
            s.UseMove(0);
            int playerHp = s.Player.Hp, npcHp = s.Npc.Hp;

            Assert.AreEqual(RunOutcome.CannotEscape, s.TryRun());

            Assert.AreEqual(playerHp, s.Player.Hp);
            Assert.AreEqual(npcHp, s.Npc.Hp);
            Assert.DoesNotThrow(() => s.UseMove(0));
        }

        [Test]
        public void TryRun_Collection_Escapes_ThenNextBattleBringsNewOpponent()
        {
            var picks = new Picks(9, 7);
            var s = Session(25, picks);
            s.Start();

            Assert.AreEqual(RunOutcome.Escaped, s.TryRun());
            Assert.IsTrue(s.IsOver, "an escaped battle is over (no winner)");
            Assert.IsNull(s.State.Winner);

            s.NextBattle();
            Assert.AreEqual(7, s.Npc.Species.Id);
            Assert.DoesNotThrow(() => s.UseMove(0));
        }

        [Test]
        public void TryRun_Collection_WithOnlyOneOpponent_CannotEscape()
        {
            // 상대 후보가 1마리면 도망쳐도 같은 상대가 HP 가득으로 다시 나와, 지는 판을 되돌리는 수단이 된다 (9.2)
            var s = new BattleSession(Catalog(), Rules, new SeededRandom(3), BattleMode.Collection, 25, () => 9, canEscape: false);
            s.Start();
            Assert.AreEqual(RunOutcome.CannotEscape, s.TryRun());
            Assert.IsFalse(s.IsOver);
        }

        [Test]
        public void CollectionPlayers_AreOwnedAndPlayable_ShinyIfAnyShinyOwned()
        {
            // 보유 ∩ 플레이어 가능 (9.2): 9 는 가졌어도 playable 이 아니라 상대로만 나온다. 로스터에 없는 151 은 무시.
            var c = Catalog(Q("[ { 'id': 25, 'tier': 'showcase', 'npcWeight': 10, 'playable': true }, { 'id': 9, 'tier': 'showcase', 'npcWeight': 10 }, " +
                              "{ 'id': 26, 'tier': 'standard', 'npcWeight': 0, 'playable': true } ]"));
            var owned = new[] { new OwnedSticker { id = 9 }, new OwnedSticker { id = 25 }, new OwnedSticker { id = 25, shiny = true }, new OwnedSticker { id = 151 } };

            CollectionAssert.AreEqual(new[] { new PlayerChoice(25, Shiny: true) }, CollectionLineup.Players(c, owned));
            CollectionAssert.IsEmpty(CollectionLineup.Players(c, null));
        }

        [Test]
        public void SameSeed_ReproducesWholeSession()
        {
            static List<BattleEvent> Script(uint seed)
            {
                var c = Catalog();
                var random = new SeededRandom(seed);
                var s = new BattleSession(c, Rules, random, BattleMode.Collection, 26, OpponentPicker.Weighted(c, random, excludeId: 26));
                var log = new List<BattleEvent>(s.Start());
                for (int round = 0; round < 4; round++)
                {
                    for (int i = 0; !s.State.IsOver && i < 100; i++) log.AddRange(s.UseMove(i % 2));
                    log.AddRange(s.State.Winner == BattleState.Player ? s.NextBattle() : s.Retry());
                }
                return log;
            }

            var a = Script(20261016);
            Assert.That(a.Count, Is.GreaterThan(10));
            CollectionAssert.AreEqual(a, Script(20261016));
        }

        [Test]
        public void Weighted_MapsRandomValueToCumulativeWeights()
        {
            var roster = Q("[ { 'id': 25, 'tier': 'showcase', 'npcWeight': 1 }, { 'id': 9, 'tier': 'showcase', 'npcWeight': 3 }, { 'id': 7, 'tier': 'standard', 'npcWeight': 6 } ]");
            // 누적: 25 → [0,1), 9 → [1,4), 7 → [4,10)
            var pick = OpponentPicker.Weighted(Catalog(roster), ScriptedRandom.Of(0, 1, 3, 4, 9));
            CollectionAssert.AreEqual(new[] { 25, 9, 9, 7, 7 }, Enumerable.Range(0, 5).Select(_ => pick()));
        }

        [Test]
        public void Weighted_NeverPicksZeroWeightOrExcluded()
        {
            var c = Catalog(); // 150 은 가중치 0
            var pick = OpponentPicker.Weighted(c, new SeededRandom(9), excludeId: 25);
            CollectionAssert.AreEqual(new[] { 7, 9 }, Enumerable.Range(0, 2000).Select(_ => pick()).Distinct().OrderBy(x => x).ToArray());
            Assert.AreEqual(2, OpponentPicker.CandidateCount(c, excludeId: 25));

            var onlyExcluded = Q("[ { 'id': 25, 'tier': 'showcase', 'npcWeight': 5 }, { 'id': 150, 'tier': 'standard', 'npcWeight': 0 } ]");
            Assert.Throws<InvalidOperationException>(() => OpponentPicker.Weighted(Catalog(onlyExcluded), new SeededRandom(1), excludeId: 25));
        }
    }
}
