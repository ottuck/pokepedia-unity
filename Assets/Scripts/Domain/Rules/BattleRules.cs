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

        /// <summary>압축 계수. 1.0 이면 압축하지 않는다.</summary>
        public double CompressionK { get; init; } = 0.6;

        /// <summary>급소 확률 1/N</summary>
        public int CritChanceDenominator { get; init; } = 16;

        public double CritMultiplier { get; init; } = 1.5;

        /// <summary>자속 보정(기술 타입이 사용자 타입과 같을 때)</summary>
        public double StabMultiplier { get; init; } = 1.5;

        /// <summary>데미지 난수 하한(%). 상한은 100.</summary>
        public int RandomMinPercent { get; init; } = 85;
    }
}
