using NUnit.Framework;
using Pokepedia.App.Battle;
using Pokepedia.Domain;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    public class NpcBattleAiTests
    {
        static readonly BattleRules Rules = BattleRules.Default;

        static int Choose(PokemonType selfType, PokemonType targetType, params Move[] moves)
            => NpcBattleAi.Choose(TestMon.Battler(TestMon.Stats(), selfType, moves), TestMon.Battler(TestMon.Stats(), targetType), Rules).MoveIndex;

        [Test]
        public void AccuracyLowersExpectation()
        {
            // 위력 100·명중 50 의 기대값은 위력 60·명중 100 보다 작다.
            Assert.AreEqual(1, Choose(PokemonType.Fire, PokemonType.Normal,
                TestMove.Of(PokemonType.Normal, power: 100, accuracy: 50, id: "wild"), TestMove.Of(PokemonType.Normal, power: 60, id: "sure")));
        }

        [Test]
        public void SuperEffective_BeatsStrongerNeutral()
        {
            // 물(위력 40) → 불꽃 대상은 2배 → 노말 위력 60 보다 기대값이 크다.
            Assert.AreEqual(1, Choose(PokemonType.Electric, PokemonType.Fire,
                TestMove.Of(PokemonType.Normal, power: 60, id: "normal"), TestMove.Of(PokemonType.Water, power: 40, id: "water")));
        }

        [Test]
        public void TurnRotation_MixesInWeakerMoves()
        {
            // 같은 기술만 쓰면 전투가 단조롭다: 턴 1·3 은 가장 센 기술, 2 는 두 번째, 4 는 세 번째, 이후 반복 (난수 없음)
            var self = TestMon.Battler(TestMon.Stats(), PokemonType.Fire,
                TestMove.Of(PokemonType.Normal, power: 20, id: "weak"), TestMove.Of(PokemonType.Normal, power: 60, id: "strong"),
                TestMove.Of(PokemonType.Normal, power: 40, id: "mid"));
            var target = TestMon.Battler(TestMon.Stats(), PokemonType.Normal);
            var picks = new int[5];
            for (int turn = 1; turn <= 5; turn++) picks[turn - 1] = NpcBattleAi.Choose(self, target, Rules, turn).MoveIndex;
            CollectionAssert.AreEqual(new[] { 1, 2, 1, 0, 1 }, picks);
        }

        [Test]
        public void Ties_PickFirstMove()
        {
            Assert.AreEqual(0, Choose(PokemonType.Fire, PokemonType.Normal,
                TestMove.Of(PokemonType.Normal, power: 40, id: "a"), TestMove.Of(PokemonType.Normal, power: 40, id: "b")));
        }
    }
}
