using System;

namespace Pokepedia.Domain.Rules
{
    /// <summary>
    /// 종족값 → 레벨 50 실제 능력치 (PROJECT.md 6.4, 6.5).
    /// 1) 압축: base' = round(Center + (base − Center) × K), 최소 1
    /// 2) HP = ⌊(2×base' + IV) × L / 100⌋ + L + 10, 그 외 = ⌊(2×base' + IV) × L / 100⌋ + 5
    ///
    /// 공식 출처 (3세대 이후, 노력치 0·성격 보정 없음일 때):
    /// - https://bulbapedia.bulbagarden.net/wiki/Stat (Generation III onward)
    ///   ※ 2026-10-16 Bulbapedia 는 자동 조회가 막혀(HTTP 403) 직접 읽지 못했다. 아래 시뮬레이터 구현으로 확인했다.
    /// - Pokémon Showdown sim/battle.ts statModify:
    ///   HP = ⌊⌊2B + IV + ⌊EV/4⌋ + 100⌋ × L / 100 + 10⌋, 그 외 = ⌊⌊2B + IV + ⌊EV/4⌋⌋ × L / 100 + 5⌋
    ///   https://github.com/smogon/pokemon-showdown/blob/master/sim/battle.ts
    ///   L 이 정수이므로 HP 식은 ⌊(2B + IV) × L / 100⌋ + L + 10 과 같다.
    /// </summary>
    public static class StatCalculator
    {
        public static StatBlock Compute(StatBlock baseStats, BattleRules rules)
        {
            return new StatBlock(
                Hp(Compress(baseStats.Hp, rules), rules),
                Other(Compress(baseStats.Attack, rules), rules),
                Other(Compress(baseStats.Defense, rules), rules),
                Other(Compress(baseStats.SpAttack, rules), rules),
                Other(Compress(baseStats.SpDefense, rules), rules),
                Other(Compress(baseStats.Speed, rules), rules));
        }

        /// <summary>
        /// 압축만 적용한 종족값. 반올림은 사사오입(0.5 는 올림), 결과는 최소 1.
        /// - 사사오입: 문서의 "round" 를 사람이 기대하는 방식 그대로 따른다. .NET 기본 Math.Round 는
        ///   은행원 반올림(0.5 → 짝수)이라 82.5 → 82 같은 결과가 나와 직관과 어긋난다.
        /// - decimal 로 계산: 0.6 같은 K 가 double 에서 근사값이라 x.5 경계가 x.4999… 로 흔들리는 것을 막는다.
        ///   (double → decimal 변환은 유효숫자 15자리로 반올림되므로 0.6 은 정확히 0.6 이 된다.)
        /// </summary>
        public static int Compress(int baseStat, BattleRules rules)
        {
            var center = (decimal)rules.CompressionCenter;
            var k = (decimal)rules.CompressionK;
            var compressed = center + (baseStat - center) * k;
            var rounded = (int)Math.Round(compressed, MidpointRounding.AwayFromZero);
            return Math.Max(1, rounded);
        }

        static int Hp(int baseStat, BattleRules rules)
            => (2 * baseStat + rules.Iv) * rules.Level / 100 + rules.Level + 10;

        static int Other(int baseStat, BattleRules rules)
            => (2 * baseStat + rules.Iv) * rules.Level / 100 + 5;
    }
}
