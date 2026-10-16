using NUnit.Framework;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    /// <summary>
    /// 상성 배수를 인자로 받는 오버로드로 TypeChart 와 독립적으로 검증한다.
    ///
    /// 기준 상황(L50, 위력 40, 공격 100 / 방어 100, 자속 없음):
    ///   기본 = ⌊⌊⌊22×40×100⌋/100⌋/50⌋ + 2 = ⌊880/50⌋ + 2 = 19
    /// </summary>
    public class DamageCalculatorTests
    {
        static readonly BattleRules Rules = BattleRules.Default;

        // 공격자는 불꽃 타입이라 노말 기술(Tackle)에 자속이 붙지 않는다.
        static Battler NonStabAttacker(int atk = 100, int spa = 100)
            => TestMon.Battler(TestMon.Stats(atk: atk, spa: spa), PokemonType.Fire);

        static Battler NormalAttacker()
            => TestMon.Battler(TestMon.Stats(atk: 100), PokemonType.Normal);

        static Battler Defender(int def = 100, int spd = 100)
            => TestMon.Battler(TestMon.Stats(def: def, spd: spd), PokemonType.Normal);

        static DamageResult Calc(Battler a, Battler d, Move m, bool crit = false, int random = 100, double eff = 1.0)
            => DamageCalculator.Calculate(a, d, m, crit, random, Rules, eff);

        [Test]
        public void Base_NoModifiers()
        {
            var r = Calc(NonStabAttacker(), Defender(), TestMove.Tackle);
            Assert.AreEqual(19, r.Amount);
            Assert.IsFalse(r.Critical);
            Assert.IsFalse(r.Stab);
        }

        [Test]
        public void Order_CriticalThenRandomThenStab_HalfRoundsDown()
        {
            // 19 → 급소 ⌊28.5⌋=28 → 난수 ⌊28×0.85⌋=23 → 자속 34.5 → 34 (게임 방식: 0.5 는 내림)
            var r = Calc(NormalAttacker(), Defender(), TestMove.Tackle, crit: true, random: 85);
            Assert.AreEqual(34, r.Amount);
            Assert.IsTrue(r.Critical);
            Assert.IsTrue(r.Stab);
        }

        [Test]
        public void Effectiveness_IsAppliedAfterStab()
        {
            // 자속 28 → ×0.5 = 14 (상성을 먼저 하면 ⌊19/2⌋=9 → 13.5 → 13)
            Assert.AreEqual(14, Calc(NormalAttacker(), Defender(), TestMove.Tackle, eff: 0.5).Amount);
        }

        [Test]
        public void PhysicalAndSpecial_UseTheirOwnStats()
        {
            // 물리: 공격 50 / 방어 100 → 10 (특공 200·특방 10 무시). 특수: 특공 100 / 특방 100 → 19 (공격 50·방어 10 무시)
            Assert.AreEqual(10, Calc(NonStabAttacker(atk: 50, spa: 200), Defender(def: 100, spd: 10),
                TestMove.Of(PokemonType.Normal, 40, category: MoveCategory.Physical)).Amount);
            Assert.AreEqual(19, Calc(NonStabAttacker(atk: 50, spa: 100), Defender(def: 10, spd: 100),
                TestMove.Of(PokemonType.Normal, 40, category: MoveCategory.Special)).Amount);
        }

        [Test]
        public void Immune_DealsZero_NonImmune_DealsAtLeastOne()
        {
            Assert.AreEqual(0, Calc(NormalAttacker(), Defender(), TestMove.Tackle, crit: true, eff: 0.0).Amount);
            // 공격 1 / 방어 255, 위력 10: 기본 2 → 난수 1 → ×0.25 → 0 → 최소 1
            Assert.AreEqual(1, Calc(NonStabAttacker(atk: 1), Defender(def: 255),
                TestMove.Of(PokemonType.Normal, 10, category: MoveCategory.Physical), random: 85, eff: 0.25).Amount);
        }

        [Test]
        public void Expected_IsMeanDamageTimesAccuracy()
        {
            // NPC AI 용 기대값. 실수 기본 19.6 × 평균 난수 0.925 × 급소 기대 (1 + 0.5/16), 명중 80%, 자속 1.5, 상성 2
            const double neutral = 19.6 * 0.925 * 1.03125;
            var move = TestMove.Of(PokemonType.Normal, 40, accuracy: 80, category: MoveCategory.Physical);
            Assert.AreEqual(neutral * 0.8, DamageCalculator.Expected(NonStabAttacker(), Defender(), move, Rules, 1.0), 1e-9);
            Assert.AreEqual(neutral * 1.5 * 2.0, DamageCalculator.Expected(NormalAttacker(), Defender(), TestMove.Tackle, Rules, 2.0), 1e-9);
        }

        [Test]
        public void DefaultOverload_LooksUpTypeChart()
        {
            var defender = TestMon.Battler(TestMon.Stats(def: 100), new[] { PokemonType.Rock, PokemonType.Steel });
            var viaChart = DamageCalculator.Calculate(NormalAttacker(), defender, TestMove.Tackle, false, 100, Rules);
            Assert.AreEqual(0.25, viaChart.Effectiveness);
            Assert.AreEqual(Calc(NormalAttacker(), defender, TestMove.Tackle, eff: 0.25).Amount, viaChart.Amount);
        }
    }
}
