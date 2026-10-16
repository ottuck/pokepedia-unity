using System;
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
    /// </summary>
    public static class DamageCalculator
    {
        /// <param name="randomPercent">RandomMinPercent~100 사이 정수</param>
        public static DamageResult Calculate(Battler attacker, Battler defender, Move move, bool critical, int randomPercent, BattleRules rules)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// NPC AI 용 기대 데미지: 명중률 × 평균 난수 × (급소 기대 배수) × 자속 × 상성 반영. 난수를 쓰지 않는다.
        /// </summary>
        public static double Expected(Battler attacker, Battler defender, Move move, BattleRules rules)
        {
            throw new NotImplementedException();
        }
    }
}
