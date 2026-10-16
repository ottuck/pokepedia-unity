using NUnit.Framework;
using Pokepedia.Domain;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    public class StatCalculatorTests
    {
        // 종족값 출처: PokeAPI https://pokeapi.co/api/v2/pokemon/pikachu, /blastoise (2026-10-16 확인)
        static readonly StatBlock PikachuBase = new StatBlock(35, 55, 40, 50, 50, 90);
        static readonly StatBlock BlastoiseBase = new StatBlock(79, 83, 100, 85, 105, 78);

        [Test]
        public void Compute_CurrentDefault_DemoPair()
        {
            // 현재 기본값 K=0.65 (BattleRules.Default) 로 실제 게임에 쓰이는 값. PROJECT.md 6.4 예시와 같아야 한다.
            // 피카츄 압축 종족값 51/64/54/61/61/87, 거북왕 79/82/93/83/96/79
            Assert.AreEqual(0.65, BattleRules.Default.CompressionK);
            Assert.AreEqual(new StatBlock(126, 84, 74, 81, 81, 107), StatCalculator.Compute(PikachuBase, BattleRules.Default));
            Assert.AreEqual(new StatBlock(154, 102, 113, 103, 116, 99), StatCalculator.Compute(BlastoiseBase, BattleRules.Default));
        }

        [Test]
        public void Compress_Midpoint_RoundsHalfUp()
        {
            // K=0.5: 81 → 80.5, 85 → 82.5. .NET 기본 Math.Round(은행원 반올림)이면 80, 82 가 되지만 사사오입으로 81, 83.
            var rules = BattleRules.Default with { CompressionK = 0.5 };
            Assert.AreEqual(81, StatCalculator.Compress(81, rules));
            Assert.AreEqual(83, StatCalculator.Compress(85, rules));
        }

        [Test]
        public void Compress_NeverBelowOne()
        {
            var rules = BattleRules.Default with { CompressionK = 2.0 };
            Assert.AreEqual(1, StatCalculator.Compress(5, rules)); // 80 + (-75)×2 = -70 → 1
        }
    }
}
