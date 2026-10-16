using System;

namespace Pokepedia.Presentation.Audio.Synth
{
    internal enum Waveform { Sine, Square, Sawtooth, Triangle, Pulse }

    /// <summary>
    /// 위상 누적 오실레이터. 주파수는 샘플마다 받는다(자동화·LFO·FM 반영 후의 값).
    /// Web Audio 의 내장 파형은 대역 제한(주기파 합성)이라, 사각·톱니·펄스는 PolyBLEP 로 앨리어싱을 줄여 근사한다.
    /// 위상 0 에서 sine=0, square=+1 구간 시작, sawtooth=0 에서 상승, triangle=0 에서 상승(Web Audio 와 같은 방향).
    /// </summary>
    internal sealed class Oscillator
    {
        readonly Waveform wave;
        readonly double duty;
        readonly int sampleRate;
        double phase; // [0, 1)

        public Oscillator(Waveform wave, int sampleRate, double duty = 0.5)
        {
            this.wave = wave;
            this.sampleRate = sampleRate;
            this.duty = duty <= 0.01 ? 0.01 : (duty >= 0.99 ? 0.99 : duty);
        }

        public double Next(double freqHz)
        {
            double dt = freqHz / sampleRate;
            double adt = Math.Abs(dt);
            if (adt > 0.5) adt = 0.5;
            double p = phase;
            double y;
            switch (wave)
            {
                case Waveform.Sine:
                    y = Math.Sin(2 * Math.PI * p);
                    break;
                case Waveform.Triangle:
                    y = p < 0.25 ? 4 * p : (p < 0.75 ? 2 - 4 * p : 4 * p - 4);
                    break;
                case Waveform.Sawtooth:
                {
                    double q = Frac(p + 0.5);
                    y = 2 * q - 1 - PolyBlep(q, adt);
                    break;
                }
                case Waveform.Square:
                    y = (p < 0.5 ? 1.0 : -1.0) + PolyBlep(p, adt) - PolyBlep(Frac(p + 0.5), adt);
                    break;
                default: // Pulse: 8비트 펄스 채널. 직류 성분은 뺀다.
                    y = (p < duty ? 1.0 : -1.0) + PolyBlep(p, adt) - PolyBlep(Frac(p + 1 - duty), adt) - (2 * duty - 1);
                    break;
            }
            phase = Frac(phase + dt);
            return y;
        }

        static double Frac(double x) => x - Math.Floor(x);

        static double PolyBlep(double t, double dt)
        {
            if (dt <= 0) return 0;
            if (t < dt) { t /= dt; return t + t - t * t - 1; }
            if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; }
            return 0;
        }
    }

    /// <summary>사인 LFO(Web Audio 에서 OscillatorNode → GainNode(depth) → AudioParam 연결). 시작 시각에 위상 0.</summary>
    internal sealed class Lfo
    {
        public readonly double Hz;
        public readonly double Depth;
        public readonly double Start;
        public readonly double Stop;

        public Lfo(double hz, double depth, double start, double stop = double.MaxValue)
        {
            Hz = hz; Depth = depth; Start = start; Stop = stop;
        }

        public double ValueAt(double t) =>
            t < Start || t >= Stop ? 0 : Depth * Math.Sin(2 * Math.PI * Hz * (t - Start));
    }
}
