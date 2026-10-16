namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>
    /// 시제품 <c>buffers(c)</c> 의 2초짜리 화이트/브라운 노이즈 버퍼(시드 7 고정).
    /// 브라운 = (last + 0.02x) / 1.02 누적 × 3.5 — 저역이 두터운 "우르릉".
    /// </summary>
    internal sealed class NoiseBank
    {
        public readonly double[] White;
        public readonly double[] Brown;

        public NoiseBank(int sampleRate)
        {
            int n = sampleRate * 2;
            White = new double[n];
            Brown = new double[n];
            var r = new Lcg(7);
            double last = 0;
            for (int i = 0; i < n; i++)
            {
                double x = r.Next() * 2 - 1;
                White[i] = x;
                last = (last + 0.02 * x) / 1.02;
                Brown[i] = last * 3.5;
            }
        }
    }

    /// <summary>
    /// NES 노이즈 채널식 15비트 LFSR. 클록 주파수마다 비트를 하나 밀어내 ±1 을 낸다(8비트 효과음용).
    /// 시드가 고정이라 결정적이다.
    /// </summary>
    internal sealed class LfsrNoise
    {
        readonly int sampleRate;
        readonly bool shortMode;
        uint reg = 1;
        double acc;

        public LfsrNoise(int sampleRate, bool shortMode = false)
        {
            this.sampleRate = sampleRate;
            this.shortMode = shortMode;
        }

        public double Next(double clockHz)
        {
            acc += clockHz / sampleRate;
            while (acc >= 1)
            {
                acc -= 1;
                uint fb = (reg ^ (reg >> (shortMode ? 6 : 1))) & 1u;
                reg = (reg >> 1) | (fb << 14);
            }
            return (reg & 1u) != 0 ? 1.0 : -1.0;
        }
    }
}
