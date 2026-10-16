using System;

namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>
    /// 마스터 처리: 컴프레서(시제품의 DynamicsCompressorNode 근사), 룩어헤드 피크 리미터, 꼬리 정리, float 변환.
    /// </summary>
    internal static class Dynamics
    {
        /// <summary>
        /// DynamicsCompressorNode 근사. 시제품 설정: threshold −12dB, knee 6dB, ratio 8, attack 1ms, release 250ms(기본값).
        /// - 정적 곡선: threshold 아래는 그대로, [threshold, threshold+knee] 는 2차 소프트 니, 그 위는 기울기 1/ratio.
        ///   (명세는 knee 를 threshold "위" 구간으로 정의한다. Chrome 의 지수형 니 곡선 자체는 옮기지 않았다.)
        /// - 메이크업 게인: 명세대로 (1 / 곡선(0dBFS))^0.6.
        /// - 검출기: 앞으로 6ms 구간의 최대 피크를 dB 로 보고 attack/release 1차 평활(Chrome 의 프리딜레이 6ms 를 지연 없이 흉내).
        ///   Chrome 의 적응형 release 는 옮기지 않고, 호출하는 쪽이 고정 release 를 짧게 줘서 근사한다(TypeCries 참고).
        /// </summary>
        public static void Compress(double[] x, int sampleRate,
            double thresholdDb = -12, double kneeDb = 6, double ratio = 8, double attackSec = 0.001, double releaseSec = 0.25)
        {
            double makeupDb = -Curve(0, thresholdDb, kneeDb, ratio) * 0.6; // 곡선(0dB) 은 음수
            double makeup = DbToLin(makeupDb);
            double att = Math.Exp(-1.0 / (attackSec * sampleRate));
            double rel = Math.Exp(-1.0 / (releaseSec * sampleRate));
            int look = (int)Math.Round(0.006 * sampleRate);
            double grDb = 0; // 현재 게인 감소량(≤ 0)
            int n = x.Length;
            var y = new double[n];
            for (int i = 0; i < n; i++)
            {
                // 앞으로 6ms 안의 최대 피크를 본다(게인이 트랜지언트 전에 내려가 있게).
                int end = i + look < n ? i + look : n - 1;
                double lvl = 0;
                for (int j = i; j <= end; j++) { double a = Math.Abs(x[j]); if (a > lvl) lvl = a; }
                double inDb = lvl > 1e-9 ? 20 * Math.Log10(lvl) : -180;
                double target = Curve(inDb, thresholdDb, kneeDb, ratio) - inDb;
                double c = target < grDb ? att : rel;
                grDb = target + (grDb - target) * c;
                y[i] = x[i] * DbToLin(grDb) * makeup;
            }
            Array.Copy(y, x, n);
        }

        static double Curve(double inDb, double t, double w, double r)
        {
            if (inDb <= t) return inDb;
            if (inDb <= t + w) { double d = inDb - t; return inDb + (1 / r - 1) * d * d / (2 * w); }
            return t + w / 2 + w / (2 * r) + (inDb - t - w) / r;
        }

        /// <summary>
        /// 룩어헤드 브릭월 리미터: 어떤 샘플도 |x| ≤ ceiling 이 되게 한다(클리핑 방지 최종 단계).
        /// 앞으로 올 피크까지 게인을 선형으로 미리 내리고(룩어헤드 3ms), 이후 release 로 천천히 복귀한다.
        /// </summary>
        public static void Limit(double[] x, int sampleRate, double ceiling = 0.97, double releaseSec = 0.08)
        {
            int n = x.Length;
            int look = Math.Max(1, (int)Math.Round(0.003 * sampleRate));
            var req = new double[n];
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                double a = Math.Abs(x[i]);
                req[i] = a > ceiling ? ceiling / a : 1;
                if (req[i] < 1) any = true;
            }
            if (!any) return;
            double rel = 1 - Math.Exp(-1.0 / (releaseSec * sampleRate));
            double g = 1;
            for (int i = 0; i < n; i++)
            {
                // 앞의 피크 k 에 대해, 거리 (k−i) 만큼 선형으로 완화한 요구 게인 중 최소
                double m = 1;
                int end = Math.Min(n - 1, i + look);
                for (int k = i; k <= end; k++)
                {
                    double rk = req[k];
                    if (rk >= 1) continue;
                    double v = rk + (1 - rk) * (double)(k - i) / look;
                    if (v < m) m = v;
                }
                g = g + (1 - g) * rel; // release
                if (m < g) g = m;      // attack 은 룩어헤드 램프가 맡는다
                x[i] *= g;
            }
        }

        /// <summary>최댓값이 target 이 되게 배율(무음이면 그대로).</summary>
        public static void NormalizePeak(double[] x, double target)
        {
            double peak = 0;
            for (int i = 0; i < x.Length; i++) { double a = Math.Abs(x[i]); if (a > peak) peak = a; }
            if (peak < 1e-9) return;
            double k = target / peak;
            for (int i = 0; i < x.Length; i++) x[i] *= k;
        }

        /// <summary>|x| ≤ threshold 인 꼬리를 자르고, 끝 fadeSec 를 선형 페이드아웃한 float[] 를 돌려준다.</summary>
        public static float[] TrimToFloat(double[] x, int sampleRate, double threshold = 1e-4, double fadeSec = 0.005)
        {
            int last = x.Length - 1;
            while (last > 0 && Math.Abs(x[last]) <= threshold) last--;
            int len = last + 1;
            int fade = Math.Min(len, (int)Math.Round(fadeSec * sampleRate));
            var y = new float[len];
            for (int i = 0; i < len; i++)
            {
                double v = x[i];
                int fromEnd = len - 1 - i;
                if (fromEnd < fade) v *= (double)fromEnd / fade;
                if (v > 1) v = 1; else if (v < -1) v = -1; // 리미터 뒤라 걸리지 않지만 계약([-1, 1]) 보장
                y[i] = (float)v;
            }
            return y;
        }

        static double DbToLin(double db) => Math.Pow(10, db / 20);
    }
}
