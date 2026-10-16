namespace Pokepedia.Domain.Rules
{
    /// <summary>
    /// 규칙의 "수치"만 모은 설정값. 규칙 자체를 바꾸는 추상화(IRuleSet 등)가 아니다(PROJECT.md 7.1-4).
    /// 기본값은 PROJECT.md 6.4·6.5. 밸런스 조정은 이 값과 기술 데이터(위력·명중)로 한다.
    /// </summary>
    public sealed record BattleRules
    {
        public static BattleRules Default { get; } = new BattleRules();

        /// <summary>레벨 고정</summary>
        public int Level { get; init; } = 50;

        /// <summary>개체값 (노력치 0, 성격 보정 없음)</summary>
        public int Iv { get; init; } = 31;

        /// <summary>종족값 압축 중심: stat' = Center + (종족값 − Center) × K</summary>
        public double CompressionCenter { get; init; } = 80;

        /// <summary>
        /// 압축 계수. 1.0 이면 압축하지 않는다.
        /// 0.65: 피카츄 vs 거북왕 시뮬레이션(5,000판)에서 승률 68%·평균 4.9턴, K 0.64~0.68 × 대표 기술 위력 34~36 범위에서
        /// 승률이 60~70% 안에 머무는 안정 구간의 중심 (2026-10-16, BalanceTests.Sweep).
        /// 2026-10-17: Demo 목표를 85~93%로 올리면서 k 는 그대로 두고 기술 명중(10만볼트 100, 하이드로펌프 85)으로 조정했다.
        /// </summary>
        public double CompressionK { get; init; } = 0.65;

        /// <summary>급소 확률 1/N</summary>
        public int CritChanceDenominator { get; init; } = 16;

        public double CritMultiplier { get; init; } = 1.5;

        /// <summary>자속 보정(기술 타입이 사용자 타입과 같을 때)</summary>
        public double StabMultiplier { get; init; } = 1.5;

        /// <summary>데미지 난수 하한(%). 상한은 100.</summary>
        public int RandomMinPercent { get; init; } = 85;
    }
}
