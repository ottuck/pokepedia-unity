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

        static readonly BattleRules NoCompression = BattleRules.Default with { CompressionK = 1.0 };

        // 계산 예시는 K=0.6 으로 고정한다. 실제 기본값(BattleRules.Default)은 밸런스 조정으로 바뀔 수 있다.
        static readonly BattleRules K06 = BattleRules.Default with { CompressionK = 0.6 };

        [Test]
        public void Compute_WithoutCompression_MatchesDocumentedPikachu()
        {
            Assert.AreEqual(new StatBlock(110, 75, 60, 70, 70, 110), StatCalculator.Compute(PikachuBase, NoCompression));
        }

        [Test]
        public void Compute_WithoutCompression_MatchesDocumentedBlastoise()
        {
            Assert.AreEqual(new StatBlock(154, 103, 120, 105, 125, 98), StatCalculator.Compute(BlastoiseBase, NoCompression));
        }

        [Test]
        public void Compress_KOne_KeepsBaseStat()
        {
            Assert.AreEqual(35, StatCalculator.Compress(35, NoCompression));
            Assert.AreEqual(150, StatCalculator.Compress(150, NoCompression));
        }

        [TestCase(35, 53)]   // 80 + (-45)×0.6 = 53
        [TestCase(55, 65)]   // 80 - 15
        [TestCase(90, 86)]   // 80 + 6
        [TestCase(79, 79)]   // 79.4 → 79
        [TestCase(83, 82)]   // 81.8 → 82
        [TestCase(78, 79)]   // 78.8 → 79
        [TestCase(80, 80)]   // 중심은 그대로
        [TestCase(154, 124)] // 80 + 74×0.6 = 124.4 → 124
        public void Compress_K06_PullsTowardCenter(int baseStat, int expected)
        {
            Assert.AreEqual(expected, StatCalculator.Compress(baseStat, K06));
        }

        [Test]
        public void Compress_Midpoint_RoundsHalfUp()
        {
            // K=0.5: 81 → 80.5, 85 → 82.5. .NET 기본 Math.Round(은행원 반올림)이면 80, 82 가 되지만 사사오입으로 81, 83.
            var rules = BattleRules.Default with { CompressionK = 0.5 };
            Assert.AreEqual(81, StatCalculator.Compress(81, rules));
            Assert.AreEqual(83, StatCalculator.Compress(85, rules));
            Assert.AreEqual(80, StatCalculator.Compress(79, rules)); // 79.5 → 80
        }

        [Test]
        public void Compress_NeverBelowOne()
        {
            var rules = BattleRules.Default with { CompressionK = 2.0 };
            Assert.AreEqual(1, StatCalculator.Compress(5, rules)); // 80 + (-75)×2 = -70 → 1
        }

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
        public void Compute_K06_Pikachu()
        {
            // 압축 종족값 53/65/56/62/62/86
            // HP  = ⌊(106+31)×50/100⌋ + 60 = 68 + 60 = 128
            // 공격 = ⌊(130+31)/2⌋ + 5 = 85, 방어 = ⌊143/2⌋ + 5 = 76, 특공·특방 = ⌊155/2⌋ + 5 = 82, 스피드 = ⌊203/2⌋ + 5 = 106
            Assert.AreEqual(new StatBlock(128, 85, 76, 82, 82, 106), StatCalculator.Compute(PikachuBase, K06));
        }

        [Test]
        public void Compute_K06_Blastoise()
        {
            // 압축 종족값 79/82/92/83/95/79
            // HP = ⌊189/2⌋ + 60 = 154, 공격 = ⌊195/2⌋ + 5 = 102, 방어 = ⌊215/2⌋ + 5 = 112,
            // 특공 = ⌊197/2⌋ + 5 = 103, 특방 = ⌊221/2⌋ + 5 = 115, 스피드 = ⌊189/2⌋ + 5 = 99
            Assert.AreEqual(new StatBlock(154, 102, 112, 103, 115, 99), StatCalculator.Compute(BlastoiseBase, K06));
        }

        [Test]
        public void Compute_UsesRulesLevelAndIv()
        {
            // L100, IV 0, 압축 없음, 종족값 100: HP = 200 + 100 + 10 = 310, 그 외 = 200 + 5 = 205
            var rules = NoCompression with { Level = 100, Iv = 0 };
            Assert.AreEqual(new StatBlock(310, 205, 205, 205, 205, 205),
                StatCalculator.Compute(new StatBlock(100, 100, 100, 100, 100, 100), rules));
        }
    }
}
