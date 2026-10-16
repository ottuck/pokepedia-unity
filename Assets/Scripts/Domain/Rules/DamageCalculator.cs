using System;
using System.Linq;
using Pokepedia.Domain.Battle;

namespace Pokepedia.Domain.Rules
{
    /// <summary>한 번의 공격 결과</summary>
    public readonly struct DamageResult
    {
        public int Amount { get; }
        /// <summary>타입 상성 배수 (0, 0.25, 0.5, 1, 2, 4)</summary>
        public double Effectiveness { get; }
        public bool Critical { get; }
        /// <summary>자속 보정 적용 여부</summary>
        public bool Stab { get; }

        public DamageResult(int amount, double effectiveness, bool critical, bool stab)
        {
            Amount = amount; Effectiveness = effectiveness; Critical = critical; Stab = stab;
        }
    }

    /// <summary>
    /// 데미지 공식 (PROJECT.md 6.4):
    /// ((2L/5+2) × 위력 × A/D ÷ 50 + 2) × 급소 × 난수(0.85~1.0) × 자속 × 상성
    /// A/D 는 물리면 공격/방어, 특수면 특공/특방. 난수와 급소 여부는 호출자(엔진)가 IRandom 으로 뽑아 넘긴다.
    ///
    /// 단계별 정수 처리 (6세대 이후 게임 방식, 이 프로젝트에 없는 날씨·화상·더블 배틀 보정은 뺐다):
    ///   1. 기본 = ⌊⌊⌊⌊2L/5 + 2⌋ × 위력 × A⌋ / D⌋ / 50⌋ + 2
    ///   2. 급소: ⌊x × CritMultiplier⌋            (내림)
    ///   3. 난수: ⌊x × R / 100⌋, R ∈ [85, 100]    (내림)
    ///   4. 자속: x × StabMultiplier 를 4096 고정소수점으로 곱하고 0.5 는 내림(round half down)
    ///   5. 상성: ×2 단계는 정확히 곱하고 ×0.5 단계마다 내림 → ⌊x × 상성⌋ 과 같다(0.25 = 두 번 내림 = ⌊x/4⌋)
    ///   6. 상성 0 이면 0, 그 외 최소 1
    ///
    /// 출처:
    /// - https://bulbapedia.bulbagarden.net/wiki/Damage (Generation V onward)
    ///   ※ 2026-10-16 Bulbapedia 는 자동 조회가 막혀(HTTP 403) 직접 읽지 못했다. 같은 문서를 인용한 Smogon 글
    ///     (https://www.smogon.com/forums/posts/10071081/)에 따르면 기본 데미지 이후의 보정은 따로 명시가 없으면
    ///     "가장 가까운 정수로 반올림, 0.5 는 내림"이다. 단계 순서·급소/난수의 내림은 아래 시뮬레이터로 확인했다.
    /// - Pokémon Showdown sim/battle-actions.ts getDamage·modifyDamage, sim/battle.ts modify·randomizer
    ///   https://github.com/smogon/pokemon-showdown/blob/master/sim/battle-actions.ts
    ///   https://github.com/smogon/pokemon-showdown/blob/master/sim/battle.ts
    /// - 급소 6세대 이후 ×1.5: https://bulbapedia.bulbagarden.net/wiki/Critical_hit
    /// </summary>
    public static class DamageCalculator
    {
        /// <param name="randomPercent">RandomMinPercent~100 사이 정수</param>
        public static DamageResult Calculate(Battler attacker, Battler defender, Move move, bool critical, int randomPercent, BattleRules rules)
            => Calculate(attacker, defender, move, critical, randomPercent, rules, TypeChart.Effectiveness(move.Type, defender.Types));

        /// <summary>상성 배수를 직접 넘기는 버전. TypeChart 와 분리해 테스트하거나 배수를 이미 구한 호출자가 쓴다.</summary>
        /// <param name="effectiveness">타입 상성 배수 (0, 0.25, 0.5, 1, 2, 4)</param>
        public static DamageResult Calculate(Battler attacker, Battler defender, Move move, bool critical, int randomPercent, BattleRules rules, double effectiveness)
        {
            if (randomPercent < rules.RandomMinPercent || randomPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(randomPercent), randomPercent, $"must be {rules.RandomMinPercent}..100");

            var stab = IsStab(attacker, move);
            if (effectiveness == 0) return new DamageResult(0, effectiveness, critical, stab);

            var (attack, defense) = AttackAndDefense(attacker, defender, move);
            int damage = (2 * rules.Level / 5 + 2) * move.Power * attack / defense / 50 + 2;

            if (critical) damage = (int)Math.Floor(damage * rules.CritMultiplier);
            damage = damage * randomPercent / 100;
            if (stab) damage = ApplyModifierRoundHalfDown(damage, rules.StabMultiplier);
            damage = (int)Math.Floor(damage * effectiveness);

            return new DamageResult(Math.Max(1, damage), effectiveness, critical, stab);
        }

        /// <summary>
        /// NPC AI 용 기대 데미지: 명중률 × 평균 난수 × (급소 기대 배수) × 자속 × 상성 반영. 난수를 쓰지 않는다.
        /// </summary>
        public static double Expected(Battler attacker, Battler defender, Move move, BattleRules rules)
            => Expected(attacker, defender, move, rules, TypeChart.Effectiveness(move.Type, defender.Types));

        /// <summary>
        /// 상성 배수를 직접 넘기는 버전. 비교용 추정치라 내림 없이 실수로 계산한다:
        /// 명중률/100 × (RandomMin+100)/200 × (1 + (급소배수−1)/급소분모) × 자속 × 상성 × ((2L/5+2) × 위력 × A/D / 50 + 2)
        /// </summary>
        public static double Expected(Battler attacker, Battler defender, Move move, BattleRules rules, double effectiveness)
        {
            if (effectiveness == 0) return 0;

            var (attack, defense) = AttackAndDefense(attacker, defender, move);
            var baseDamage = (2.0 * rules.Level / 5 + 2) * move.Power * attack / defense / 50 + 2;
            var accuracy = move.Accuracy / 100.0;
            var averageRandom = (rules.RandomMinPercent + 100) / 200.0;
            var expectedCrit = 1 + (rules.CritMultiplier - 1) / rules.CritChanceDenominator;
            var stab = IsStab(attacker, move) ? rules.StabMultiplier : 1.0;

            return accuracy * averageRandom * expectedCrit * stab * effectiveness * baseDamage;
        }

        static bool IsStab(Battler attacker, Move move) => attacker.Types.Contains(move.Type);

        static (int attack, int defense) AttackAndDefense(Battler attacker, Battler defender, Move move)
            => move.Category == MoveCategory.Physical
                ? (attacker.Stats.Attack, defender.Stats.Defense)
                : (attacker.Stats.SpAttack, defender.Stats.SpDefense);

        /// <summary>
        /// 게임의 보정 곱셈: 배수를 4096 분의 정수로 바꾼 뒤 곱하고, 결과의 0.5 는 내린다(round half down).
        /// Showdown battle.modify: ⌊(⌊value × ⌊m × 4096⌋⌋ + 2047) / 4096⌋
        /// </summary>
        static int ApplyModifierRoundHalfDown(int value, double multiplier)
        {
            long modifier = (long)Math.Floor(multiplier * 4096);
            return (int)((value * modifier + 2047) / 4096);
        }
    }
}
