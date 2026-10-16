namespace Pokepedia.Domain
{
    /// <summary>
    /// 엔진이 쓰는 모든 난수(명중·급소·데미지 난수·NPC 선택)의 출처.
    /// 테스트에서는 고정 값 스텁이나 시드 고정 구현을 주입한다. (Java 의 java.util.random.RandomGenerator 와 같은 역할)
    /// </summary>
    public interface IRandom
    {
        /// <summary>[0, maxExclusive) 범위의 정수</summary>
        int Next(int maxExclusive);

        /// <summary>[0, 1) 범위의 실수</summary>
        double NextDouble();
    }

    /// <summary>
    /// 시드 고정 난수 (xorshift32). 같은 시드면 플랫폼과 무관하게 같은 수열을 낸다.
    /// System.Random 은 .NET 버전에 따라 알고리즘이 달라질 수 있어 시뮬레이션 재현용으로 직접 구현한다.
    /// </summary>
    public sealed class SeededRandom : IRandom
    {
        uint state;

        public SeededRandom(uint seed) => state = seed == 0 ? 0x9E3779B9u : seed;

        uint NextUInt()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new System.ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        public double NextDouble() => NextUInt() / 4294967296.0;
    }
}
