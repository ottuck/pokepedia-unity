using NUnit.Framework;
using Pokepedia.App.Battle;
using Pokepedia.Domain;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    public class NpcBattleAiTests
    {
        static readonly BattleRules Rules = BattleRules.Default;

        [Test]
        public void PicksHighestExpectedDamage()
        {
            var weak = TestMove.Of(PokemonType.Normal, power: 30, id: "weak");
            var strong = TestMove.Of(PokemonType.Normal, power: 60, id: "strong");
            var self = TestMon.Battler(TestMon.Stats(), PokemonType.Fire, weak, strong);
            var target = TestMon.Battler(TestMon.Stats(), PokemonType.Normal);
            Assert.AreEqual(1, NpcBattleAi.Choose(self, target, Rules).MoveIndex);
        }

        [Test]
        public void AccuracyLowersExpectation()
        {
            // 위력 100·명중 50 의 기대값은 위력 60·명중 100 보다 작다.
            var wild = TestMove.Of(PokemonType.Normal, power: 100, accuracy: 50, id: "wild");
            var sure = TestMove.Of(PokemonType.Normal, power: 60, accuracy: 100, id: "sure");
            var self = TestMon.Battler(TestMon.Stats(), PokemonType.Fire, wild, sure);
            var target = TestMon.Battler(TestMon.Stats(), PokemonType.Normal);
            Assert.AreEqual(1, NpcBattleAi.Choose(self, target, Rules).MoveIndex);
        }

        [Test]
        public void Ties_PickFirstMove()
        {
            var a = TestMove.Of(PokemonType.Normal, power: 40, id: "a");
            var b = TestMove.Of(PokemonType.Normal, power: 40, id: "b");
            var self = TestMon.Battler(TestMon.Stats(), PokemonType.Fire, a, b);
            var target = TestMon.Battler(TestMon.Stats(), PokemonType.Normal);
            Assert.AreEqual(0, NpcBattleAi.Choose(self, target, Rules).MoveIndex);
        }

        [Test]
        public void SuperEffective_BeatsStrongerNeutral()
        {
            // 물(위력 40) → 불꽃 대상은 2배 → 노말 위력 60 보다 기대값이 크다.
            var water = TestMove.Of(PokemonType.Water, power: 40, id: "water");
            var normal = TestMove.Of(PokemonType.Normal, power: 60, id: "normal");
            var self = TestMon.Battler(TestMon.Stats(), PokemonType.Electric, normal, water);
            var target = TestMon.Battler(TestMon.Stats(), PokemonType.Fire);
            Assert.AreEqual(1, NpcBattleAi.Choose(self, target, Rules).MoveIndex);
        }
    }
}
