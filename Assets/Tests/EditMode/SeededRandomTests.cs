using NUnit.Framework;
using Pokepedia.Domain;

namespace Pokepedia.Tests
{
    // 이 폴더의 테스트는 UnityEngine 을 쓰지 않는다 → Unity Test Runner 와 dotnet test 양쪽에서 같은 파일이 돈다.
    public class SeededRandomTests
    {
        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.Next(1000), b.Next(1000));
        }

        [Test]
        public void KnownSequence_IsStableAcrossPlatforms()
        {
            // xorshift32(seed=1) 의 첫 값들. 플랫폼이 바뀌어도 이 값이 같아야 시뮬레이션이 재현된다.
            var r = new SeededRandom(1);
            Assert.AreEqual(270369 % 100, r.Next(100));
            Assert.AreEqual(67634689u % 100, (uint)r.Next(100));
        }

        [Test]
        public void NextDouble_IsInUnitRange()
        {
            var r = new SeededRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                var d = r.NextDouble();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
            }
        }

        [Test]
        public void Next_RejectsNonPositiveBound()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new SeededRandom(1).Next(0));
        }
    }
}
