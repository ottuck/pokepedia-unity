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
        public void Ties_PickFirstMove()
        {
            Assert.AreEqual(0, Choose(PokemonType.Fire, PokemonType.Normal,
                TestMove.Of(PokemonType.Normal, power: 40, id: "a"), TestMove.Of(PokemonType.Normal, power: 40, id: "b")));
        }
    }
}
