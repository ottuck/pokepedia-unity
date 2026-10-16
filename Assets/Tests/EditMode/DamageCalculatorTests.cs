using System;
using NUnit.Framework;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    /// <summary>
    /// 상성 배수를 인자로 받는 오버로드로 TypeChart 와 독립적으로 검증한다.
    /// TypeChart 를 실제로 호출하는 테스트는 [Category("NeedsTypeChart")].
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

        // ---- Calculate: 기본·난수·급소·자속 ----

        [Test]
        public void Base_NoModifiers()
        {
            var r = Calc(NonStabAttacker(), Defender(), TestMove.Tackle);
            Assert.AreEqual(19, r.Amount);
            Assert.AreEqual(1.0, r.Effectiveness);
            Assert.IsFalse(r.Critical);
            Assert.IsFalse(r.Stab);
        }

        [Test]
        public void Random_IsFlooredPercent()
        {
            // ⌊19 × 85 / 100⌋ = ⌊16.15⌋ = 16
            Assert.AreEqual(16, Calc(NonStabAttacker(), Defender(), TestMove.Tackle, random: 85).Amount);
        }

        [Test]
        public void Random_OutOfRange_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Calc(NonStabAttacker(), Defender(), TestMove.Tackle, random: 84));
            Assert.Throws<ArgumentOutOfRangeException>(() => Calc(NonStabAttacker(), Defender(), TestMove.Tackle, random: 101));
        }

        [Test]
        public void Critical_MultipliesAndFloors()
        {
            // ⌊19 × 1.5⌋ = ⌊28.5⌋ = 28
            var r = Calc(NonStabAttacker(), Defender(), TestMove.Tackle, crit: true);
            Assert.AreEqual(28, r.Amount);
            Assert.IsTrue(r.Critical);
        }

        [Test]
        public void Stab_RoundsHalfDown()
        {
            // 19 × 1.5 = 28.5 → 게임 방식(4096 고정소수점, 0.5 는 내림) = 28. 사사오입이면 29 가 된다.
            var r = Calc(NormalAttacker(), Defender(), TestMove.Tackle);
            Assert.AreEqual(28, r.Amount);
            Assert.IsTrue(r.Stab);
        }

        [Test]
        public void Stab_AppliesForEitherOfDualTypes()
        {
            var attacker = TestMon.Battler(TestMon.Stats(atk: 100), new[] { PokemonType.Fire, PokemonType.Normal });
            var r = Calc(attacker, Defender(), TestMove.Tackle);
            Assert.IsTrue(r.Stab);
            Assert.AreEqual(28, r.Amount);
        }

        [Test]
        public void Order_RandomIsAppliedBeforeStab()
        {
            // 난수 먼저: ⌊19×0.85⌋ = 16 → 16×1.5 = 24
            // (자속 먼저였다면 28 → ⌊28×0.85⌋ = 23)
            Assert.AreEqual(24, Calc(NormalAttacker(), Defender(), TestMove.Tackle, random: 85).Amount);
        }

        [Test]
        public void Order_CriticalThenRandomThenStab()
        {
            // 19 → 급소 28 → 난수 ⌊28×0.85⌋ = 23 → 자속 34.5 → 34 (0.5 내림)
            var r = Calc(NormalAttacker(), Defender(), TestMove.Tackle, crit: true, random: 85);
            Assert.AreEqual(34, r.Amount);
            Assert.IsTrue(r.Critical);
            Assert.IsTrue(r.Stab);
        }

        // ---- Calculate: 물리/특수 ----

        [Test]
        public void Physical_UsesAttackAndDefense()
        {
            // 공격 50 / 방어 100: ⌊22×40×50/100⌋ = 440 → ⌊440/50⌋ + 2 = 10 (특공·특방은 무시)
            var r = Calc(NonStabAttacker(atk: 50, spa: 200), Defender(def: 100, spd: 10),
                TestMove.Of(PokemonType.Normal, 40, category: MoveCategory.Physical));
            Assert.AreEqual(10, r.Amount);
        }

        [Test]
        public void Special_UsesSpAttackAndSpDefense()
        {
            // 특공 100 / 특방 100 → 19 (공격 50·방어 10 은 무시)
            var r = Calc(NonStabAttacker(atk: 50, spa: 100), Defender(def: 10, spd: 100),
                TestMove.Of(PokemonType.Normal, 40, category: MoveCategory.Special));
            Assert.AreEqual(19, r.Amount);
        }

        [Test]
        public void BaseDamage_FloorsEachDivision()
        {
            // 특공 70, 특방 125, 위력 90: ⌊22×90×70/125⌋ = ⌊1108.8⌋ = 1108 → ⌊1108/50⌋ + 2 = 24
            // (실수로 끝까지 계산하면 24.176… 이라 결과는 같지만 단계별 내림을 고정)
            var r = Calc(NonStabAttacker(spa: 70), Defender(spd: 125), TestMove.Of(PokemonType.Electric, 90));
            Assert.AreEqual(24, r.Amount);
        }

        // ---- Calculate: 상성 (1배 결과 × 배수) ----

        [TestCase(2.0, 38)]   // 19 × 2
        [TestCase(4.0, 76)]   // 19 × 4
        [TestCase(0.5, 9)]    // ⌊19 / 2⌋
        [TestCase(0.25, 4)]   // ⌊⌊19 / 2⌋ / 2⌋ = ⌊19 / 4⌋
        public void Effectiveness_ScalesNeutralResult(double eff, int expected)
        {
            var neutral = Calc(NonStabAttacker(), Defender(), TestMove.Tackle).Amount;
            var r = Calc(NonStabAttacker(), Defender(), TestMove.Tackle, eff: eff);
            Assert.AreEqual(19, neutral);
            Assert.AreEqual(expected, r.Amount);
            Assert.AreEqual(eff, r.Effectiveness);
        }

        [Test]
        public void Effectiveness_IsAppliedAfterStab()
        {
            // 자속 28 → ×0.5 = 14 (상성을 먼저 하면 ⌊19/2⌋=9 → 13.5 → 13)
            Assert.AreEqual(14, Calc(NormalAttacker(), Defender(), TestMove.Tackle, eff: 0.5).Amount);
        }

        [Test]
        public void Immune_DealsZero()
        {
            var r = Calc(NormalAttacker(), Defender(), TestMove.Tackle, crit: true, eff: 0.0);
            Assert.AreEqual(0, r.Amount);
            Assert.AreEqual(0.0, r.Effectiveness);
        }

        [Test]
        public void NonImmune_DealsAtLeastOne()
        {
            // 공격 1 / 방어 255, 위력 10: ⌊220/255⌋ = 0 → 0/50 + 2 = 2 → 난수 ⌊1.7⌋ = 1 → ×0.25 → 0 → 최소 1
            var r = Calc(NonStabAttacker(atk: 1), Defender(def: 255),
                TestMove.Of(PokemonType.Normal, 10, category: MoveCategory.Physical), random: 85, eff: 0.25);
            Assert.AreEqual(1, r.Amount);
        }

        [Test]
        public void Example_PikachuThunderboltOnBlastoise()
        {
            // 압축 없는 L50 능력치: 피카츄 특공 70, 거북왕 특방 125. 10만볼트(위력 90, 특수, 전기) 자속, 상성 ×2, 난수 100
            // 기본 24 → 자속 36 → ×2 = 72
            var pikachu = TestMon.Battler(TestMon.Stats(spa: 70), PokemonType.Electric);
            var blastoise = TestMon.Battler(TestMon.Stats(spd: 125), PokemonType.Water);
            var r = DamageCalculator.Calculate(pikachu, blastoise, TestMove.Of(PokemonType.Electric, 90), false, 100, Rules, 2.0);
            Assert.AreEqual(72, r.Amount);
            Assert.IsTrue(r.Stab);
        }

        [Test]
        public void UsesRulesMultipliers()
        {
            var rules = Rules with { CritMultiplier = 2.0, StabMultiplier = 2.0 };
            // 19 → 급소 38 → 자속 76
            var r = DamageCalculator.Calculate(NormalAttacker(), Defender(), TestMove.Tackle, true, 100, rules, 1.0);
            Assert.AreEqual(76, r.Amount);
        }

        // ---- Expected (NPC AI) ----
        // 실수 기본 = 22×40×100/100/50 + 2 = 19.6, 평균 난수 0.925, 급소 기대 1 + 0.5/16 = 1.03125

        const double NeutralExpected = 19.6 * 0.925 * 1.03125;

        [Test]
        public void Expected_Neutral()
        {
            Assert.AreEqual(NeutralExpected, DamageCalculator.Expected(NonStabAttacker(), Defender(), TestMove.Tackle, Rules, 1.0), 1e-9);
        }

        [Test]
        public void Expected_ScalesWithAccuracy()
        {
            var move = TestMove.Of(PokemonType.Normal, 40, accuracy: 80, category: MoveCategory.Physical);
            Assert.AreEqual(NeutralExpected * 0.8, DamageCalculator.Expected(NonStabAttacker(), Defender(), move, Rules, 1.0), 1e-9);
        }

        [Test]
        public void Expected_AppliesStabAndEffectiveness()
        {
            Assert.AreEqual(NeutralExpected * 1.5 * 2.0, DamageCalculator.Expected(NormalAttacker(), Defender(), TestMove.Tackle, Rules, 2.0), 1e-9);
            Assert.AreEqual(NeutralExpected * 0.25, DamageCalculator.Expected(NonStabAttacker(), Defender(), TestMove.Tackle, Rules, 0.25), 1e-9);
        }

        [Test]
        public void Expected_Immune_IsZero()
        {
            Assert.AreEqual(0.0, DamageCalculator.Expected(NormalAttacker(), Defender(), TestMove.Tackle, Rules, 0.0));
        }

        [Test]
        public void Expected_UsesSpecialStats()
        {
            // 특공 50 / 특방 100: 22×40×0.5/50 + 2 = 10.8
            var move = TestMove.Of(PokemonType.Normal, 40);
            Assert.AreEqual(10.8 * 0.925 * 1.03125,
                DamageCalculator.Expected(NonStabAttacker(atk: 100, spa: 50), Defender(def: 100, spd: 100), move, Rules, 1.0), 1e-9);
        }

        // ---- TypeChart 를 실제로 호출하는 원래 시그니처 ----

        [Test, Category("NeedsTypeChart")]
        public void Calculate_LooksUpTypeChart_NormalVsNormalIsNeutral()
        {
            var r = DamageCalculator.Calculate(NonStabAttacker(), Defender(), TestMove.Tackle, false, 100, Rules);
            Assert.AreEqual(19, r.Amount);
            Assert.AreEqual(1.0, r.Effectiveness);
        }

        [Test, Category("NeedsTypeChart")]
        public void Calculate_LooksUpTypeChart_MatchesExplicitEffectiveness()
        {
            var attacker = NormalAttacker();
            var defender = TestMon.Battler(TestMon.Stats(def: 100), new[] { PokemonType.Rock, PokemonType.Steel });
            var eff = TypeChart.Effectiveness(PokemonType.Normal, defender.Types);
            var viaChart = DamageCalculator.Calculate(attacker, defender, TestMove.Tackle, false, 100, Rules);
            var explicitEff = DamageCalculator.Calculate(attacker, defender, TestMove.Tackle, false, 100, Rules, eff);
            Assert.AreEqual(explicitEff.Amount, viaChart.Amount);
            Assert.AreEqual(eff, viaChart.Effectiveness);
        }

        [Test, Category("NeedsTypeChart")]
        public void Expected_LooksUpTypeChart_NormalVsNormalIsNeutral()
        {
            Assert.AreEqual(NeutralExpected, DamageCalculator.Expected(NonStabAttacker(), Defender(), TestMove.Tackle, Rules), 1e-9);
        }
    }
}
