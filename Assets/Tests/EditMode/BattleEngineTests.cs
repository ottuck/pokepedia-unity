using System.Linq;
using NUnit.Framework;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    // 엔진이 IRandom 을 뽑는 순서 (ScriptedRandom 으로 고정):
    //   1) 턴 순서: 스피드가 같을 때만 Next(2) — 0 이면 플레이어 먼저
    //   2) 행동마다: 명중률 < 100 이면 Next(100) (값 < 명중률이면 명중)
    //   3) 명중하면: 급소 Next(CritChanceDenominator) (0 이면 급소) → 데미지 난수 Next(101 - RandomMinPercent)
    public class BattleEngineTests
    {
        static readonly BattleRules Rules = BattleRules.Default;

        static BattleEngine Engine(Battler player, Battler npc, IRandom random)
            => new BattleEngine(new BattleState(new BattleSide(player), new BattleSide(npc)), Rules, random);

        static Battler Mon(int spe = 50, int hp = 100, PokemonType type = PokemonType.Normal, params Move[] moves)
            => TestMon.Battler(TestMon.Stats(hp: hp, spe: spe), type, moves);

        static readonly UseMove First = new UseMove(0);

        [Test]
        public void Start_SendsOutNpcThenPlayer()
        {
            var e = Engine(Mon(hp: 120), Mon(hp: 90), ScriptedRandom.Of());
            Assert.AreEqual(new BattleEvent[]
            {
                new PokemonSentOut(BattleState.Npc, 1, 90, 90),
                new PokemonSentOut(BattleState.Player, 1, 120, 120),
            }, e.Start());
        }

        [Test]
        public void FasterSideActsFirst()
        {
            // 플레이어 스피드 40 < NPC 60 → NPC 먼저. 명중 100 이라 명중 난수 없음.
            var rng = ScriptedRandom.Of(5, 3, /*player*/ 5, 3);
            var e = Engine(Mon(spe: 40), Mon(spe: 60), rng);
            e.Start();
            var events = e.PlayTurn(First, First);

            Assert.AreEqual(new TurnStarted(1), events[0]);
            Assert.AreEqual(new[] { BattleState.Npc, BattleState.Player }, events.OfType<MoveUsed>().Select(m => m.Side).ToArray());
            Assert.AreEqual(0, rng.Remaining, "명중 100 기술은 명중 난수를 뽑지 않는다");
        }

        [TestCase(0, BattleState.Player)]
        [TestCase(1, BattleState.Npc)]
        public void SpeedTie_IsBrokenByRandom(int roll, int expectedFirst)
        {
            var e = Engine(Mon(spe: 50), Mon(spe: 50), ScriptedRandom.Of(roll, 5, 0, 5, 0));
            e.Start();
            Assert.AreEqual(expectedFirst, e.PlayTurn(First, First).OfType<MoveUsed>().First().Side);
        }

        [Test]
        public void Miss_WhenAccuracyRollIsNotBelowAccuracy()
        {
            var shaky = TestMove.Of(PokemonType.Normal, power: 40, accuracy: 90, category: MoveCategory.Physical, id: "shaky");
            // 플레이어가 빠름. 플레이어: 명중 난수 90 (>= 90 → 빗나감). NPC: 태클(명중 100) 급소 5, 난수 0.
            var rng = ScriptedRandom.Of(90, 5, 0);
            var npc = Mon(spe: 10);
            var e = Engine(Mon(spe: 99, moves: shaky), npc, rng);
            e.Start();
            var events = e.PlayTurn(First, First);

            CollectionAssert.Contains(events, new MoveMissed(BattleState.Player, "shaky"));
            Assert.AreEqual(npc.MaxHp, npc.Hp, "빗나가면 피해가 없다");
            Assert.AreEqual(0, rng.Remaining, "빗나가면 급소·데미지 난수를 뽑지 않는다");
        }

        [Test]
        public void Damage_UsesCritAndRandomRolls_FromCalculator()
        {
            var player = Mon(spe: 99);
            var npc = Mon(spe: 10, hp: 500);
            // 플레이어: 급소 0(급소!), 난수 7 → 92%. NPC: 급소 3(아님), 난수 15 → 100%.
            var e = Engine(player, npc, ScriptedRandom.Of(0, 7, 3, 15));
            e.Start();

            var expectedToNpc = DamageCalculator.Calculate(player, npc, TestMove.Tackle, true, 92, Rules);
            var expectedToPlayer = DamageCalculator.Calculate(npc, player, TestMove.Tackle, false, 100, Rules);
            var dealt = e.PlayTurn(First, First).OfType<DamageDealt>().ToArray();

            Assert.AreEqual(new DamageDealt(BattleState.Npc, expectedToNpc.Amount, 500 - expectedToNpc.Amount, 1.0, true), dealt[0]);
            Assert.AreEqual(new DamageDealt(BattleState.Player, expectedToPlayer.Amount, 100 - expectedToPlayer.Amount, 1.0, false), dealt[1]);
        }

        [Test]
        public void LethalHit_FaintsTarget_EndsBattle_AndSlowerSideDoesNotAct()
        {
            var nuke = TestMove.Of(PokemonType.Normal, power: 250, category: MoveCategory.Physical, id: "nuke");
            var npc = Mon(spe: 10, hp: 5);
            var e = Engine(Mon(spe: 99, moves: nuke), npc, ScriptedRandom.Of(5, 15));
            e.Start();

            Assert.AreEqual(new BattleEvent[]
            {
                new TurnStarted(1),
                new MoveUsed(BattleState.Player, "nuke"),
                new DamageDealt(BattleState.Npc, 5, 0, 1.0, false), // 남은 HP 만큼만 깎인다
                new Fainted(BattleState.Npc),
                new BattleEnded(BattleState.Player),
            }, e.PlayTurn(First, First));
            Assert.AreEqual(BattleState.Player, e.State.Winner);
        }

        [Test]
        public void Immune_DealsZero_AndIsNeverCritical()
        {
            // 노말 → 고스트는 0배. 급소가 떠도 "급소에 맞았다!" 를 띄우지 않는다.
            var npc = Mon(spe: 10, type: PokemonType.Ghost, moves: TestMove.Of(PokemonType.Ghost, id: "spook"));
            var e = Engine(Mon(spe: 99), npc, ScriptedRandom.Of(0, 15, /*npc*/ 5, 0));
            e.Start();
            var hit = e.PlayTurn(First, First).OfType<DamageDealt>().First();
            Assert.AreEqual(new DamageDealt(BattleState.Npc, 0, npc.MaxHp, 0.0, false), hit);
        }
    }
}
