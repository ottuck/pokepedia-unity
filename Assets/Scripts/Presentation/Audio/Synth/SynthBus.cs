using System;

namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>
    /// 오프라인 렌더 한 번의 작업 공간. 모든 버스(double[])는 같은 길이이고, 시각 0 = 버퍼 시작이다.
    /// 시제품의 Web Audio 노드 연결(소스 → 필터 → 게인 엔벨로프 → 출력)을 "출력 버퍼에 더하기" 로 바꾼 도우미를 모아 둔다.
    /// 소스가 멈춘 뒤의 필터 잔향은 계산하지 않는다(시제품의 모든 경로는 그 시점에 게인이 0 또는 0.0001 이다).
    /// </summary>
    internal sealed class SynthBus
    {
        public readonly int SampleRate;
        public readonly int Length;
        readonly NoiseBank noise;

        public SynthBus(int sampleRate, double seconds)
        {
            SampleRate = sampleRate;
            Length = Math.Max(1, (int)Math.Ceiling(seconds * sampleRate));
            noise = new NoiseBank(sampleRate);
        }

        public double[] White => noise.White;
        public double[] Brown => noise.Brown;

        public double[] NewBuffer() => new double[Length];

        public double TimeOf(int n) => (double)n / SampleRate;

        /// <summary>시각 t 이상인 첫 샘플(Web Audio 의 start 와 같이 다음 프레임부터 시작).</summary>
        public int IndexAtOrAfter(double t)
        {
            double x = Math.Ceiling(t * SampleRate - 1e-9);
            if (x < 0) return 0;
            return x > Length ? Length : (int)x;
        }

        /// <summary>시제품 <c>env()</c>: 첫 점에서 setValue, 이후 점까지 선형 램프. 시각은 t0 기준 상대값.</summary>
        public static Automation Env(double t0, double defaultValue, params (double t, double v)[] pts)
        {
            var a = new Automation(defaultValue);
            a.Set(pts[0].v, t0 + pts[0].t);
            for (int i = 1; i < pts.Length; i++) a.LinearTo(pts[i].v, t0 + pts[i].t);
            return a;
        }

        // ---------------------------------------------------------------- 오실레이터 경로

        /// <summary>오실레이터(주파수 자동화 + LFO + detune) → [선택: 바이쿼드] → 게인 자동화 → dest.</summary>
        public void Osc(double[] dest, Waveform wave, double start, double stop, Automation freq, Automation gain,
            double detuneCents = 0, Lfo freqLfo = null,
            FilterType? filterType = null, double filterQ = 1, Automation filterFreq = null, Lfo filterLfo = null,
            double duty = 0.5, Lfo gainLfo = null)
        {
            int a = IndexAtOrAfter(start), b = IndexAtOrAfter(stop);
            var osc = new Oscillator(wave, SampleRate, duty);
            Biquad bq = filterType.HasValue ? new Biquad(filterType.Value, filterQ, SampleRate) : null;
            double detuneMul = Math.Pow(2, detuneCents / 1200);
            for (int n = a; n < b; n++)
            {
                double t = TimeOf(n);
                double f = freq.ValueAt(t);
                if (freqLfo != null) f += freqLfo.ValueAt(t);
                double y = osc.Next(f * detuneMul);
                if (bq != null)
                {
                    double ff = filterFreq.ValueAt(t);
                    if (filterLfo != null) ff += filterLfo.ValueAt(t);
                    y = bq.Process(y, ff);
                }
                double g = gain.ValueAt(t);
                if (gainLfo != null) g += gainLfo.ValueAt(t);
                dest[n] += y * g;
            }
        }

        /// <summary>시제품 <c>voice()</c>: env 로 주파수·게인, 선택적 비브라토, 마지막 게인 점 + 0.05초에 정지.</summary>
        public void Voice(double[] dest, double t0, Waveform wave, (double t, double v)[] freqPts, (double t, double v)[] gainPts,
            double vibHz = 0, double vibDepth = 0, double detuneCents = 0)
        {
            double stop = t0 + gainPts[gainPts.Length - 1].t + 0.05;
            Lfo vib = vibHz != 0 ? new Lfo(vibHz, vibDepth, t0, stop) : null;
            Osc(dest, wave, t0, stop, Env(t0, 440, freqPts), Env(t0, 0, gainPts), detuneCents, vib);
        }

        /// <summary>시제품 <c>blip()</c>: 지수 피치 이동 + 3ms 어택, 지수 감쇠.</summary>
        public void Blip(double[] dest, double t, double f0, double f1, double dur, double peak, Waveform wave = Waveform.Sine)
        {
            var freq = new Automation(440).Set(f0, t).ExpTo(f1, t + dur);
            var gain = new Automation(0).Set(0, t).LinearTo(peak, t + 0.003).ExpTo(0.0001, t + dur);
            Osc(dest, wave, t, t + dur + 0.02, freq, gain);
        }

        // ---------------------------------------------------------------- 노이즈 경로

        /// <summary>노이즈 버퍼 재생(시작 오프셋, 반복 여부) → 바이쿼드(주파수 자동화 + LFO) → 게인 자동화 → dest.</summary>
        public void Noise(double[] dest, double[] buf, double start, double stop, double offset, bool loop,
            FilterType type, double q, Automation filterFreq, Automation gain, Lfo filterLfo = null)
        {
            int a = IndexAtOrAfter(start), b = IndexAtOrAfter(stop);
            var bq = new Biquad(type, q, SampleRate);
            int len = buf.Length;
            int off = (int)Math.Round(offset * SampleRate);
            for (int n = a; n < b; n++)
            {
                int idx = off + (n - a);
                double x;
                if (loop) x = buf[idx % len];
                else x = idx < len ? buf[idx] : 0; // 반복 없는 버퍼는 끝나면 무음
                double t = TimeOf(n);
                double ff = filterFreq.ValueAt(t);
                if (filterLfo != null) ff += filterLfo.ValueAt(t);
                dest[n] += bq.Process(x, ff) * gain.ValueAt(t);
            }
        }

        /// <summary>시제품 <c>src()</c> + 필터 + 게인: 반복 재생, 오프셋 % 1.8, 길이 + 0.05초에 정지.</summary>
        public void Src(double[] dest, double[] buf, double t, double dur, double offset,
            FilterType type, double q, Automation filterFreq, Automation gain, Lfo filterLfo = null)
        {
            Noise(dest, buf, t, t + dur + 0.05, offset % 1.8, true, type, q, filterFreq, gain, filterLfo);
        }

        /// <summary>시제품 <c>grain()</c>: 노이즈 → 고정 필터 → min(2ms, dur×0.3) 선형 어택, dur 까지 0.0001 로 지수 감쇠.</summary>
        public void Grain(double[] dest, double t, double dur, double peak,
            FilterType type = FilterType.Bandpass, double f = 2000, double q = 1, double off = 0, double[] buf = null)
        {
            var gain = new Automation(0).Set(0, t).LinearTo(peak, t + Math.Min(0.002, dur * 0.3)).ExpTo(0.0001, t + dur);
            Noise(dest, buf ?? White, t, t + dur + 0.02, off % 1.8, false, type, q, Automation.Constant(f), gain);
        }

        // ---------------------------------------------------------------- 효과

        /// <summary>
        /// 시제품의 메아리: mix → dest, mix → Delay ⇄ (feedback gain) , Delay → dest.
        /// dest += input + e,  e[n] = line[n − D],  line[n] = input[n] + fb·e[n] (소수 지연은 선형 보간).
        /// </summary>
        public void FeedbackDelay(double[] input, double delaySec, double feedback, double[] dest)
        {
            double d = delaySec * SampleRate;
            int di = (int)Math.Floor(d);
            double frac = d - di;
            var line = new double[Length];
            for (int n = 0; n < Length; n++)
            {
                double e = 0;
                int i0 = n - di, i1 = n - di - 1;
                if (i0 >= 0) e += line[i0] * (1 - frac);
                if (i1 >= 0) e += line[i1] * frac;
                line[n] = input[n] + feedback * e;
                dest[n] += input[n] + e;
            }
        }

        /// <summary>시제품 <c>clipCurve(k)</c> 웨이브셰이퍼: tanh(k·x) / tanh(k), 입력은 [-1, 1] 로 고정.</summary>
        public static double TanhShape(double x, double k)
        {
            if (x > 1) x = 1; else if (x < -1) x = -1;
            return Math.Tanh(k * x) / Math.Tanh(k);
        }
    }
}
