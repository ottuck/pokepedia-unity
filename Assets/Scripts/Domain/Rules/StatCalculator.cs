using System;

namespace Pokepedia.Domain.Rules
{
    /// <summary>
    /// 종족값 → 레벨 50 실제 능력치 (PROJECT.md 6.4, 6.5).
    /// 1) 압축: base' = round(Center + (base − Center) × K), 최소 1
    /// 2) HP = ⌊(2×base' + IV) × L / 100⌋ + L + 10, 그 외 = ⌊(2×base' + IV) × L / 100⌋ + 5
    /// </summary>
    public static class StatCalculator
    {
        public static StatBlock Compute(StatBlock baseStats, BattleRules rules)
        {
            throw new NotImplementedException();
        }

        /// <summary>압축만 적용한 종족값</summary>
        public static int Compress(int baseStat, BattleRules rules)
        {
            throw new NotImplementedException();
        }
    }
}
