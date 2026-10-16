namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>
    /// 시제품 HTML 의 <c>rng(seed)</c> 를 그대로 옮긴 선형 합동 난수(Numerical Recipes 상수).
    /// JS 의 <c>(s * 1664525 + 1013904223) &gt;&gt;&gt; 0</c> 는 2^53 안에서 정확히 계산된 뒤 2^32 로 나눈 나머지라
    /// uint 오버플로 연산과 같다. 그래서 같은 시드면 그레인 배치·노이즈 버퍼가 시제품과 똑같이 나온다.
    /// (Domain 의 SeededRandom(xorshift32) 대신 이걸 쓰는 이유: 사용자가 승인한 소리의 알갱이 배치를 그대로 재현하기 위해)
    /// </summary>
    internal sealed class Lcg
    {
        uint state;

        public Lcg(uint seed) => state = seed;

        /// <summary>[0, 1) 범위의 실수. JS <c>s / 4294967296</c> 와 같다.</summary>
        public double Next()
        {
            unchecked { state = state * 1664525u + 1013904223u; }
            return state / 4294967296.0;
        }
    }
}
