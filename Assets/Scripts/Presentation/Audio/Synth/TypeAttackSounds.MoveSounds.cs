using System;

namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>
    /// 기술 하나에 붙이는 전용 공격음 (animation.json 의 moveSfx 값). 지정이 없는 기술은 타입 공격음을 쓴다.
    /// 쓸 수 있는 id: 이 파일에서 새로 만든 소리(<see cref="MoveOnlyIds"/>) + 보관한 소리(<see cref="AlternateIds"/>).
    /// 체인은 보관한 소리와 같다(출력 0.8 → 컴프레서 → 리미터).
    /// </summary>
    public static partial class TypeAttackSounds
    {
        /// <summary>기술 전용으로 새로 만든 소리 id</summary>
        public static readonly string[] MoveOnlyIds = { "tackle", "water_geyser" };

        /// <summary>몸통박치기: 소리 시작(= 돌진 시작) 뒤 부딪히는 시각(초). AnimationDirector 의 돌진 시간(0.09초)과 같다.</summary>
        public const double TackleImpactSec = 0.09;

        /// <summary>물기둥(물의맹세): 둘레 작은 물기둥 간격(초). 큰 물기둥은 4개 뒤. AnimationDirector 의 hydro_geyser 시간표와 같다.</summary>
        const double GeyserRingGap = 0.12;

        public static bool IsMoveSound(string id)
            => Array.IndexOf(MoveOnlyIds, id) >= 0 || Array.IndexOf(AlternateIds, id) >= 0;

        public static float[] RenderMoveSound(string id, double lengthMul = 1, int sampleRate = DefaultSampleRate)
        {
            switch (id)
            {
                case "tackle": return RenderRecipe(Tackle, 1.0, 0, lengthMul, sampleRate);
                case "water_geyser": return RenderRecipe(WaterGeyser, 1.0, 0, lengthMul, sampleRate);
                default:
                    if (Array.IndexOf(AlternateIds, id) >= 0) return RenderAlternate(id, 0, lengthMul, sampleRate);
                    throw new ArgumentException("기술 공격음 id 가 아닙니다: " + id + " (" + string.Join(", ", MoveOnlyIds) + ", " + string.Join(", ", AlternateIds) + ")");
            }
        }

        /// <summary>
        /// 소리 시작부터 "발사 순간"까지(초). 연출은 발사 순간보다 이만큼 먼저 튼다.
        /// 전기 방전만 자체 충전 뒤에 크랙이 난다. 몸통박치기는 돌진 시작, 물기둥은 첫 물기둥에 맞춰 튼다.
        /// </summary>
        public static double MoveSoundLead(string id, double lengthMul) => id == "electric_discharge" ? 0.22 * lengthMul : 0;

        // 몸통박치기: 슈욱 돌진(밴드패스가 위로 쓸림 + 낮은 몸놀림) → 퍽! 몸통 충돌(딸깍 어택 + 중역 퍽 + 묵직한 저음 쿵)
        // → 흙먼지 쓸림과 작은 파편. 충돌 시각은 길이 배수와 관계없이 TackleImpactSec(연출의 돌진 시간이 고정이라).
        static void Tackle(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(29);
            double h = t + TackleImpactSec;

            c.Src(d, c.White, t, TackleImpactSec + 0.01, 0.3, FilterType.Bandpass, 1.2, // 1) 슈욱
                new Automation(700).Set(700 * p, t).ExpTo(2600 * p, h),
                SynthBus.Env(t, 0, (0, 0.05 * v), (TackleImpactSec * 0.9, 0.55 * v), (TackleImpactSec + 0.01, 0)));
            c.Src(d, c.Brown, t, TackleImpactSec, 0.9, FilterType.Lowpass, 1, Automation.Constant(420 * p),
                SynthBus.Env(t, 0, (0, 0), (TackleImpactSec, 0.35 * v), (TackleImpactSec + 0.01, 0)));

            c.Grain(d, h, 0.006, 0.9 * v, FilterType.Highpass, 2500, 1, 0.2);            // 2) 딸깍(어택)
            c.Grain(d, h, 0.07, 1.0 * v, FilterType.Bandpass, 1100 * p, 0.9, 0.7);       //    퍽
            c.Blip(d, h, 150 * p, 42 * p, 0.22, 1.1 * v);                                //    쿵(몸통)
            c.Blip(d, h + 0.004, 95 * p, 36 * p, 0.3, 0.55 * v, Waveform.Triangle);
            c.Grain(d, h, 0.16, 0.9 * v, FilterType.Lowpass, 380 * p, 1, 0.5, c.Brown);

            double dust = 0.22 * L / 0.8;                                                 // 3) 흙먼지 쓸림
            c.Src(d, c.White, h + 0.03, dust, 1.1, FilterType.Bandpass, 0.7, Automation.Constant(2400 * p),
                SynthBus.Env(h + 0.03, 0, (0, 0), (0.02, 0.22 * v), (dust, 0)));
            for (int i = 0; i < 7; i++)                                                  //    작은 파편
                c.Grain(d, h + 0.04 + r.Next() * 0.22, 0.004 + 0.006 * r.Next(), (0.1 + 0.12 * r.Next()) * v,
                    FilterType.Highpass, 3000 + 2000 * r.Next(), 1, r.Next() * 1.7);
        }

        // 물기둥(물의맹세): 물기둥이 솟을 때마다 "촤아" 한 번 — 둘레 작은 물기둥 4개는 0.12초 간격으로 점점 세게,
        // 큰 물기둥은 마지막에 더 크고 길게(촤아 촤아 촤아 촤아 촤아아). 시각은 연출과 같게(길이 배수 무시).
        static void WaterGeyser(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(53);
            for (int k = 0; k < 4; k++) Splash(c, d, r, t + k * GeyserRingGap, p * (1.08 - 0.04 * k), 0.16, (0.6 + 0.1 * k) * v);
            Splash(c, d, r, t + 4 * GeyserRingGap, p * 0.92, 0.55, 1.05 * v);
        }

        /// <summary>
        /// 물보라 한 번 "촤아". 물소리는 거품의 공명(Minnaert 공명: 거품 하나 = 위로 미끄러지는 짧은 사인)이 바탕이고
        /// 물살은 출렁이는 대역 노이즈로 만든다(van den Doel 의 거품 모델, 신스 효과음 관례).
        /// 짧게 터졌다 바로 줄어드는 노이즈(10ms 안팎)는 총소리·스네어로 들리므로 40ms 에 걸쳐 차오르게 하고,
        /// 그 뒤 지수로 잦아들어 다음 물기둥 전에 충분히 작아지게 한다(그래서 하나씩 끊어져 들린다).
        /// </summary>
        static void Splash(SynthBus c, double[] d, Lcg r, double s, double p, double dur, double a)
        {
            const double rise = 0.045;
            double tau = dur / 3;
            // 처음엔 아주 작게 시작해 곡선으로 차오른다(직선이면 컴프레서가 앞을 눌러 순간적으로 터지는 것처럼 들린다)
            Func<double, double> env = tt => tt < rise ? (tt / rise) * (tt / rise) : Math.Exp(-(tt - rise) / tau);
            double len = dur + 0.1;

            c.Src(d, c.White, s, len, r.Next() * 1.7, FilterType.Bandpass, 0.9,            // 촤아(출렁이는 물살, 대역이 위로)
                new Automation(700).Set(700 * p, s).ExpTo(1800 * p, s + rise + 0.05).ExpTo(1100 * p, s + len),
                Flutter(r, s, len, 0.01, tt => a * env(tt), 0.6));
            c.Src(d, c.White, s + 0.02, len, r.Next() * 1.7, FilterType.Highpass, 0.7,     // 물보라 쏴
                Automation.Constant(3800 * p), Flutter(r, s + 0.02, len, 0.008, tt => 0.3 * a * env(tt), 0.5));
            c.Src(d, c.Brown, s, len, r.Next() * 1.7, FilterType.Lowpass, 2,                // 꿀렁
                Automation.Constant(450 * p), Flutter(r, s, len, 0.03, tt => 0.45 * a * env(tt), 0.7));
            int n = (int)(14 + 40 * dur);                                                  // 거품 구름(차오른 뒤에 몰림)
            for (int i = 0; i < n; i++)
            {
                double u = r.Next() * r.Next();
                double bt = s + rise * 0.5 + u * dur;
                Bubble(c, d, bt, (500 + 2000 * r.Next()) * p, 1.15 + 0.35 * r.Next(), 0.015 + 0.03 * r.Next(),
                    (0.08 + 0.12 * r.Next()) * a * (1 - 0.7 * u));
            }
        }
    }
}
