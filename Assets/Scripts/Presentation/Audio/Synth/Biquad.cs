using System;

namespace Pokepedia.Presentation.Audio.Synth
{
    internal enum FilterType { Lowpass, Highpass, Bandpass }

    /// <summary>
    /// Web Audio <c>BiquadFilterNode</c> 와 같은 RBJ(Audio EQ Cookbook) 계수.
    /// 명세대로 lowpass/highpass 의 Q 는 dB 값(α = sin w0 / (2·10^(Q/20))), bandpass 의 Q 는 선형 값(α = sin w0 / 2Q)이다.
    /// 주파수가 바뀔 때만 계수를 다시 계산하므로 샘플마다 주파수를 넘겨도 된다(a-rate 자동화).
    /// </summary>
    internal sealed class Biquad
    {
        readonly FilterType type;
        readonly double q;
        readonly int sampleRate;
        double lastFreq = double.NaN;
        double b0, b1, b2, a1, a2;
        double x1, x2, y1, y2;

        public Biquad(FilterType type, double q, int sampleRate)
        {
            this.type = type;
            this.q = q;
            this.sampleRate = sampleRate;
        }

        public double Process(double x, double freqHz)
        {
            if (freqHz != lastFreq) Update(freqHz);
            double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x;
            y2 = y1; y1 = y;
            return y;
        }

        void Update(double freqHz)
        {
            lastFreq = freqHz;
            double nyquist = sampleRate * 0.5;
            double f = freqHz < 1 ? 1 : (freqHz > nyquist * 0.999 ? nyquist * 0.999 : freqHz);
            double w0 = 2 * Math.PI * f / sampleRate;
            double cs = Math.Cos(w0), sn = Math.Sin(w0);
            double nb0, nb1, nb2, na0, na1, na2;
            switch (type)
            {
                case FilterType.Lowpass:
                {
                    double alpha = sn / (2 * Math.Pow(10, q / 20));
                    nb0 = (1 - cs) / 2; nb1 = 1 - cs; nb2 = (1 - cs) / 2;
                    na0 = 1 + alpha; na1 = -2 * cs; na2 = 1 - alpha;
                    break;
                }
                case FilterType.Highpass:
                {
                    double alpha = sn / (2 * Math.Pow(10, q / 20));
                    nb0 = (1 + cs) / 2; nb1 = -(1 + cs); nb2 = (1 + cs) / 2;
                    na0 = 1 + alpha; na1 = -2 * cs; na2 = 1 - alpha;
                    break;
                }
                default:
                {
                    double alpha = sn / (2 * q);
                    nb0 = alpha; nb1 = 0; nb2 = -alpha;
                    na0 = 1 + alpha; na1 = -2 * cs; na2 = 1 - alpha;
                    break;
                }
            }
            b0 = nb0 / na0; b1 = nb1 / na0; b2 = nb2 / na0;
            a1 = na1 / na0; a2 = na2 / na0;
        }
    }
}
