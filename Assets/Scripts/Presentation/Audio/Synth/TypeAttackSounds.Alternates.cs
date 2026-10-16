using System;

namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>
    /// 보관한 속성 공격음: M3-3 에서 2안 중 고르지 않은 쪽. 지금 게임은 쓰지 않지만(굽지도 않는다)
    /// 속성마다 두 번째 기술을 추가할 때 그대로 쓸 수 있는 품질이라 남겨 둔다(사용자 요청, 2026-10-17).
    /// 쓰려면 AudioBaker 에서 <see cref="RenderAlternate"/> 로 구워 키를 연결한다. 들어 보기: Tools/audio/render 의 alt_*.wav.
    /// 착탄 시각은 만들 때 맞춘 연출 기준(L = 0.8): 물줄기 0.62L, 발사형 구체 0.44L.
    /// </summary>
    public static partial class TypeAttackSounds
    {
        /// <summary>보관한 소리 id: 속성_이름</summary>
        public static readonly string[] AlternateIds = { "water_magic", "fire_flamethrower", "grass_root_burst" };

        /// <summary>보관한 소리를 게임과 같은 체인(출력 0.8 → 컴프레서 → 리미터)으로 렌더한다.</summary>
        public static float[] RenderAlternate(string id, double semitones = 0, double lengthMul = 1, int sampleRate = DefaultSampleRate)
        {
            switch (id)
            {
                case "water_magic": return RenderRecipe(WaterMagic, 1.4, semitones, lengthMul, sampleRate);
                case "fire_flamethrower": return RenderRecipe(FireFlamethrower, 1.0, semitones, lengthMul, sampleRate);
                case "grass_root_burst": return RenderRecipe(GrassRootBurst, 1.8, semitones, lengthMul, sampleRate);
                default: throw new ArgumentException("보관한 공격음 id 가 아닙니다: " + id + " (" + string.Join(", ", AlternateIds) + ")");
            }
        }

        /// <summary>레시피 하나(보조 속성 없음)를 게임 체인으로 렌더한다.</summary>
        static float[] RenderRecipe(Recipe recipe, double loud, double semitones, double lengthMul, int sampleRate)
        {
            if (!(lengthMul > 0)) throw new ArgumentOutOfRangeException(nameof(lengthMul));
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            var bus = new SynthBus(sampleRate, 1.6 * lengthMul + 0.8);
            double[] d = bus.NewBuffer();
            recipe(bus, d, 0, Math.Pow(2, semitones / 12), lengthMul, loud);
            for (int i = 0; i < d.Length; i++) d[i] *= 0.8;
            Dynamics.Compress(d, sampleRate, releaseSec: 0.08);
            Dynamics.Limit(d, sampleRate);
            return Dynamics.TrimToFloat(d, sampleRate);
        }

        // 물 "물 마법": 거품이 차오르며 음이 올라감 → 물줄기 쏴아(난류) → 착탄 첨벙 + 거품 폭발 → 물방울 똑똑. 위에 옅은 마법 반짝임.
        static void WaterMagic(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(41);
            double hit = 0.62 * L, T = 1.05 * L;

            for (int i = 0; i < 42; i++) // 1) 차오르는 거품: 뒤로 갈수록 촘촘하고 높게
            {
                double u = Math.Sqrt(r.Next());
                double bt = t + u * hit * 0.95;
                double f0 = (280 + 900 * u + 300 * r.Next()) * p;
                Bubble(c, d, bt, f0, 1.6 + 0.9 * r.Next(), 0.02 + 0.04 * r.Next(), (0.05 + 0.13 * u) * v);
            }

            double s0 = t + 0.06 * L, sd = hit - 0.06 * L + 0.05; // 2) 물줄기 몸통
            c.Src(d, c.White, s0, sd, 0.3, FilterType.Bandpass, 1.3,
                new Automation(800 * p).Set(800 * p, s0).ExpTo(1900 * p, s0 + sd),
                Flutter(r, s0, sd, 0.012, tt => 0.55 * v * Math.Min(1, tt / (0.3 * L)), 0.65));
            c.Src(d, c.Brown, s0, sd, 0.9, FilterType.Lowpass, 1, Automation.Constant(550 * p),
                Flutter(r, s0, sd, 0.02, tt => 0.5 * v * Math.Min(1, tt / (0.25 * L)), 0.5));

            double h = t + hit; // 3) 착탄 첨벙
            c.Grain(d, h, 0.28, 0.85 * v, FilterType.Highpass, 1100, 1, 0.4);
            c.Grain(d, h, 0.16, 0.6 * v, FilterType.Lowpass, 2800, 1, 1.2, c.Brown);
            c.Blip(d, h, 190 * p, 62 * p, 0.14, 0.5 * v); // 퐁(물 덩어리)
            for (int i = 0; i < 34; i++) // 거품 폭발
            {
                double bt = h + Math.Pow(r.Next(), 1.8) * 0.3;
                Bubble(c, d, bt, (700 + 2300 * r.Next()) * p, 1.8 + r.Next(), 0.012 + 0.03 * r.Next(), (0.06 + 0.12 * r.Next()) * v);
            }
            for (int i = 0; i < 9; i++) // 4) 물방울
            {
                double bt = h + 0.1 + r.Next() * (T - hit - 0.1);
                Bubble(c, d, bt, (1300 + 1400 * r.Next()) * p, 2.3, 0.03, (0.05 + 0.07 * r.Next()) * v);
            }

            double[] mix = c.NewBuffer(); // 5) 마법 반짝임(옅게)
            double[] ks = { 1, 1.335, 1.5, 2 };
            for (int i = 0; i < ks.Length; i++)
                c.Blip(mix, t + 0.04 * L + i * 0.06 * L, 880 * ks[i] * p, 880 * ks[i] * p * 1.01, 0.16, 0.04 * v, Waveform.Triangle);
            c.FeedbackDelay(mix, 0.11, 0.3, d);
        }

        // 불 "화염 방사": 점화 후웅 → 거칠게 타오르는 굉음이 커짐(쉬익 + 타닥) → 콰앙 → 계속 활활 타다가 잦아듦.
        static void FireFlamethrower(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(13);
            double hit = 0.44 * L, T = 1.15 * L;

            c.Blip(d, t, 110 * p, 48 * p, 0.14, 0.55 * v, Waveform.Sine); // 1) 점화 "후웅"
            c.Src(d, c.Brown, t, 0.12 * L, 0.3, FilterType.Lowpass, 1,
                new Automation(400).Set(400, t).ExpTo(1600 * p, t + 0.1 * L),
                SynthBus.Env(t, 0, (0, 0), (0.05 * L, 0.7 * v), (0.12 * L, 0.3 * v)));

            double[] roar = c.NewBuffer(); // 2) 굉음 몸통: 커졌다가(착탄까지) 천천히 잦아듦
            Func<double, double> body = tt => tt < hit ? 0.35 + 0.65 * tt / hit : Math.Max(0, 1 - (tt - hit) / (T - hit));
            c.Src(roar, c.Brown, t + 0.02, T, 0.6, FilterType.Lowpass, 1, Automation.Constant(950 * p),
                Flutter(r, t + 0.02, T, 0.025, tt => 0.8 * v * body(tt), 0.55));
            AddGritty(d, roar, 2.2, 0.55);
            c.Src(d, c.White, t + 0.03, T * 0.85, 1.1, FilterType.Bandpass, 0.7, Automation.Constant(2600 * p), // 쉬익
                Flutter(r, t + 0.03, T * 0.85, 0.02, tt => 0.22 * v * body(tt), 0.6));
            Crackle(c, d, r, t + 0.04, t + T, 70, 0.4 * v, 1.3);

            double h = t + hit; // 3) 콰앙
            c.Blip(d, h, 150 * p, 34, 0.36, 0.75 * v);
            c.Grain(d, h, 0.4, 1.0 * v, FilterType.Lowpass, 1500 * p, 1, 1.1, c.Brown);
            c.Grain(d, h, 0.18, 0.35 * v, FilterType.Bandpass, 1900, 0.7, 0.9);
            Crackle(c, d, r, h, h + 0.25, 25, 0.5 * v, 1.6);
        }

        // 풀 "뿌리 솟구침": 땅이 우지끈 갈라짐(저역 울림 + 쪼개짐) → 굵은 뿌리가 끄으윽 뻗음 → 휙휙 휘감아 → 꽉 조이는 끼익 → 잎사귀 흩날림.
        static void GrassRootBurst(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(91);
            double wrap = 0.44 * L, T = 1.05 * L;

            c.Src(d, c.Brown, t, 0.4 * L, 0.5, FilterType.Lowpass, 1, Automation.Constant(220 * p), // 1) 우지끈
                Flutter(r, t, 0.4 * L, 0.03, tt => 0.8 * v * Math.Max(0, 1 - tt / (0.4 * L)), 0.4));
            for (int i = 0; i < 7; i++)
                c.Grain(d, t + 0.01 + Math.Pow(r.Next(), 1.5) * 0.12 * L, 0.01 + 0.02 * r.Next(), (0.35 + 0.35 * r.Next()) * v,
                    FilterType.Highpass, 1200 + 1500 * r.Next(), 1, r.Next() * 1.7);
            Creak(c, d, r, t, t + 0.1 * L, Automation.Constant(160), SynthBus.Env(t, 0, (0, 0.5 * v), (0.1 * L, 0)), 600 * p, 1500 * p, 2800 * p, 0.3);

            Creak(c, d, r, t + 0.08 * L, t + wrap + 0.05, // 2) 끄으윽 뻗는 뿌리(낮고 굵게)
                new Automation(16).Set(16, t + 0.08 * L).ExpTo(42, t + wrap),
                SynthBus.Env(t + 0.08 * L, 0, (0, 0), (0.2 * L, 0.6 * v), (wrap - 0.08 * L + 0.05, 0.3 * v)),
                170 * p, 480 * p, 1050 * p, 0.25);

            Swish(c, d, t + 0.2 * L, 0.12 * L, 400 * p, 3200 * p, 0.4 * v, 0.9); // 3) 휘감기
            Swish(c, d, t + 0.31 * L, 0.12 * L, 520 * p, 4200 * p, 0.45 * v, 1.3);

            double h = t + wrap; // 4) 꽉
            c.Grain(d, h, 0.1, 1.4 * v, FilterType.Bandpass, 850 * p, 10, 0.5);
            c.Blip(d, h, 160 * p, 55 * p, 0.14, 0.45 * v, Waveform.Sine);
            Creak(c, d, r, h, h + 0.28 * L,
                new Automation(75).Set(75, h).ExpTo(130, h + 0.2 * L),
                SynthBus.Env(h, 0, (0, 0.55 * v), (0.28 * L, 0)),
                620 * p, 1500 * p, 2700 * p, 0.12);

            for (int i = 0; i < 30; i++) // 5) 잎사귀 흩날림
                c.Grain(d, h + 0.05 + r.Next() * (T - wrap - 0.05), 0.006 + 0.014 * r.Next(), (0.04 + 0.1 * r.Next()) * v,
                    FilterType.Bandpass, 3000 + 5000 * r.Next(), 1.5, r.Next() * 1.7);
        }
    }
}
