using System;

namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>
    /// 보관한 속성 공격음: M3-3 에서 2안 중 고르지 않은 쪽. 지금 게임은 쓰지 않지만(굽지도 않는다)
    /// 속성마다 두 번째 기술을 추가할 때 그대로 쓸 수 있는 품질이라 남겨 둔다(사용자 요청, 2026-10-17).
    /// 쓰려면 AudioBaker 에서 <see cref="RenderAlternate"/> 로 구워 키를 연결한다. 들어 보기: Tools/audio/render 의 alt_*.wav.
    /// 착탄 시각은 만들 때 맞춘 연출 기준(L = 0.8): 물줄기·효과형 고리 0.62L, 발사형 구체 0.44L, 접촉 ≈ 0.12L,
    /// 전기 방전은 크랙이 0.22L(자체 충전 뒤).
    /// </summary>
    public static partial class TypeAttackSounds
    {
        /// <summary>보관한 소리 id: 속성_이름</summary>
        public static readonly string[] AlternateIds =
        {
            "water_magic", "fire_flamethrower", "grass_root_burst",
            "electric_discharge", "flying_wing_strike", "poison_toxic_bog", "ghost_shadow_ball", "psychic_telekinesis",
        };

        /// <summary>보관한 소리를 게임과 같은 체인(출력 0.8 → 컴프레서 → 리미터)으로 렌더한다.</summary>
        public static float[] RenderAlternate(string id, double semitones = 0, double lengthMul = 1, int sampleRate = DefaultSampleRate)
        {
            switch (id)
            {
                case "water_magic": return RenderRecipe(WaterMagic, 1.4, semitones, lengthMul, sampleRate);
                case "fire_flamethrower": return RenderRecipe(FireFlamethrower, 1.0, semitones, lengthMul, sampleRate);
                case "grass_root_burst": return RenderRecipe(GrassRootBurst, 1.8, semitones, lengthMul, sampleRate);
                case "electric_discharge": return RenderRecipe(ElectricDischarge, 1.0, semitones, lengthMul, sampleRate);
                case "flying_wing_strike": return RenderRecipe(FlyingWingStrike, 1.6, semitones, lengthMul, sampleRate);
                case "poison_toxic_bog": return RenderRecipe(PoisonToxicBog, 1.0, semitones, lengthMul, sampleRate);
                case "ghost_shadow_ball": return RenderRecipe(GhostShadowBall, 1.0, semitones, lengthMul, sampleRate);
                case "psychic_telekinesis": return RenderRecipe(PsychicTelekinesis, 1.3, semitones, lengthMul, sampleRate);
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

        /// <summary>tanh 로 찌그러뜨린 톱니파 "지직" 하강음(전기 방전 한 줄기).</summary>
        static void Zap(SynthBus c, double[] d, double t, double dur, double f0, double f1, double peak)
        {
            var osc = new Oscillator(Waveform.Sawtooth, c.SampleRate);
            var hp = new Biquad(FilterType.Highpass, 0.7, c.SampleRate);
            var f = new Automation(f0).Set(f0, t).ExpTo(f1, t + dur);
            var g = new Automation(0).Set(0, t).LinearTo(peak, t + 0.003).ExpTo(0.001, t + dur);
            int a = c.IndexAtOrAfter(t), b = c.IndexAtOrAfter(t + dur);
            for (int n = a; n < b; n++)
            {
                double tn = c.TimeOf(n);
                d[n] += hp.Process(SynthBus.TanhShape(osc.Next(f.ValueAt(tn)) * 3, 4), 250) * g.ValueAt(tn);
            }
        }

        // 전기 "방전": 우웅 고압 험(찌그러진 120Hz) 충전 → 파지직! 혼돈의 아크(끊길 때마다 음색이 바뀜) + 지직 방전 3줄기 → 탁 끊기고 오존 쉬이.
        static void ElectricDischarge(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(131);
            double ch = 0.22 * L, s = t + ch;

            { // 1) 고압 험: 커지며 위로 조금 들린다
                var sq = new Oscillator(Waveform.Square, c.SampleRate);
                var g = SynthBus.Env(t, 0, (0, 0), (ch, 0.35 * v));
                var f = new Automation(120).Set(120 * p, t).ExpTo(180 * p, s);
                int a0 = c.IndexAtOrAfter(t), a1 = c.IndexAtOrAfter(s);
                for (int n = a0; n < a1; n++)
                {
                    double tn = c.TimeOf(n);
                    d[n] += SynthBus.TanhShape(sq.Next(f.ValueAt(tn)) * 0.8, 3) * g.ValueAt(tn);
                }
            }
            c.Osc(d, Waveform.Sine, t, s, new Automation(1500).Set(1500 * p, t).ExpTo(4200 * p, s),
                SynthBus.Env(t, 0, (0, 0), (ch, 0.12 * v)));

            double A = 0.6 * L; // 2) 혼돈의 아크: 조각마다 밴드패스 중심이 300~4000Hz 로 튄다
            {
                var saw = new Oscillator(Waveform.Sawtooth, c.SampleRate);
                var mod = new Oscillator(Waveform.Square, c.SampleRate);
                var bp = new Biquad(FilterType.Bandpass, 2.5, c.SampleRate);
                double tt = 0;
                while (tt < A)
                {
                    double on = 0.005 + r.Next() * 0.02, off = 0.002 + r.Next() * 0.01;
                    double fc = 300 * Math.Pow(13, r.Next());
                    double amp = (0.5 + 0.5 * r.Next()) * 0.9 * v * Math.Pow(1 - tt / A, 0.8);
                    double fm = 60 + 400 * r.Next();
                    int a0 = c.IndexAtOrAfter(s + tt), a1 = c.IndexAtOrAfter(s + tt + on);
                    for (int n = a0; n < a1; n++)
                    {
                        double x = saw.Next(140 * p + 90 * mod.Next(fm));
                        double env = Math.Min(1, (n - a0) / 40.0) * Math.Min(1, (a1 - n) / 40.0);
                        d[n] += bp.Process(SynthBus.TanhShape(x, 12), fc) * amp * env * 1.4;
                    }
                    tt += on + off;
                }
            }
            Zap(c, d, s, 0.07, 2600 * p, 180 * p, 0.7 * v); // 3) 지직 방전 3줄기
            Zap(c, d, s + 0.14 * L, 0.06, 2200 * p, 160 * p, 0.55 * v);
            Zap(c, d, s + 0.3 * L, 0.08, 3000 * p, 200 * p, 0.6 * v);
            c.Grain(d, s, 0.005, 1.3 * v, FilterType.Highpass, 200, 1, 0.5);
            Crackle(c, d, r, s, s + A, 50, 0.4 * v, 1.2);

            double e = s + A; // 4) 탁 + 오존 쉬이
            c.Grain(d, e, 0.006, 0.7 * v, FilterType.Highpass, 1500, 1, 0.8);
            c.Src(d, c.White, e, 0.35 * L, 1.2, FilterType.Highpass, 1, Automation.Constant(4500),
                SynthBus.Env(e, 0, (0, 0.2 * v), (0.35 * L, 0)));
        }

        // 비행 "날개 강타": 묵직한 펄럭(저역 쿵 + 바람) → 휘익 내려치는 바람 → 소용돌이치며 흩어지는 돌풍. 맞는 순간 ≈ 0.12L(접촉).
        static void FlyingWingStrike(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(151);
            double hit = 0.12 * L, T = 0.95 * L;

            c.Src(d, c.Brown, t, 0.08, 0.3, FilterType.Lowpass, 1, Automation.Constant(450 * p), // 1) 펄럭
                SynthBus.Env(t, 0, (0, 0), (0.012, 0.9 * v), (0.08, 0)));
            c.Blip(d, t, 85 * p, 45 * p, 0.09, 0.5 * v);
            c.Src(d, c.White, t + 0.005, 0.07, 0.8, FilterType.Bandpass, 1.2, Automation.Constant(900 * p),
                SynthBus.Env(t + 0.005, 0, (0, 0), (0.015, 0.4 * v), (0.07, 0)));

            Swish(c, d, t + 0.02, hit + 0.05, 500 * p, 3500 * p, 0.75 * v, 1.1, 1.2); // 2) 휘익 내려침
            c.Src(d, c.Brown, t + hit, 0.12, 1.3, FilterType.Lowpass, 1, Automation.Constant(700 * p),
                SynthBus.Env(t + hit, 0, (0, 0.7 * v), (0.12, 0)));

            double g0 = t + hit, gd = T - hit; // 3) 소용돌이 돌풍: 밴드패스가 3.5Hz 로 오르내리며 잦아든다
            c.Src(d, c.White, g0, gd, 0.5, FilterType.Bandpass, 2.2,
                new Automation(1300 * p).Set(1300 * p, g0).ExpTo(800 * p, g0 + gd),
                Flutter(r, g0, gd, 0.03, tt => 0.45 * v * Math.Max(0, 1 - tt / gd) * Math.Min(1, tt / 0.05), 0.35),
                new Lfo(3.5, 600 * p, g0));
        }

        // 독 "독 늪": 부글부글 끓는 걸쭉한 거품 + 메스꺼운 어긋난 두 음(트레몰로) + 점점 커지는 치이익 → 꿀렁! 큰 거품이 터짐. 착탄 0.44L.
        static void PoisonToxicBog(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(211);
            double hit = 0.44 * L, T = 1.1 * L;

            for (int i = 0; i < 46; i++) // 1) 부글부글: 낮고 굵은 거품
                Bubble(c, d, t + r.Next() * T * 0.9, (110 + 380 * r.Next()) * p, 1.25 + 0.5 * r.Next(), 0.04 + 0.06 * r.Next(), (0.1 + 0.16 * r.Next()) * v);

            double[] tone = c.NewBuffer(); // 2) 메스꺼운 두 음: 196Hz 를 ±35센트로 어긋나게, 7Hz 트레몰로, 끝으로 갈수록 처진다
            foreach (double det in new[] { -35.0, 35.0 })
                c.Osc(tone, Waveform.Square, t, t + T,
                    new Automation(196).Set(196 * p, t).Set(196 * p, t + hit).ExpTo(150 * p, t + T),
                    SynthBus.Env(t, 0, (0, 0), (0.1 * L, 0.11 * v), (0.8 * L, 0.09 * v), (T, 0)),
                    det, filterType: FilterType.Lowpass, filterQ: 3, filterFreq: Automation.Constant(900 * p));
            var trem = new Lfo(7, 0.45, t);
            for (int n = 0; n < tone.Length; n++)
                if (tone[n] != 0) d[n] += tone[n] * (0.55 + trem.ValueAt(c.TimeOf(n)));

            c.Src(d, c.White, t, T, 1.1, FilterType.Highpass, 1, Automation.Constant(3500), // 3) 치이익
                Flutter(r, t, T, 0.025, tt => 0.22 * v * Math.Min(1, tt / hit) * Math.Max(0, 1.2 - tt / T), 0.6));

            double h = t + hit; // 4) 꿀렁 펑
            Bubble(c, d, h, 90 * p, 2.2, 0.16, 0.9 * v);
            c.Src(d, c.Brown, h, 0.18, 0.4, FilterType.Lowpass, 4,
                new Automation(1500).Set(1500 * p, h).ExpTo(300 * p, h + 0.16),
                SynthBus.Env(h, 0, (0, 0), (0.008, 0.8 * v), (0.18, 0)));
            Crackle(c, d, r, h, h + 0.2, 16, 0.35 * v, 1.5);
        }

        // 고스트 "섀도볼": 어긋난 저음 톱니가 우우웅 차오름(필터가 열림) + 위에서 떨며 내려오는 섬뜩한 음 → 웅! 어둠이 터짐 → 메아리. 착탄 0.62L.
        static void GhostShadowBall(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            double hit = 0.62 * L, T = 1.3 * L;

            double[] mix = c.NewBuffer();
            foreach (var (f, det) in new[] { (55.0, -9.0), (55.0, 9.0), (110.0, 4.0) }) // 1) 차오르는 어둠
                c.Osc(mix, Waveform.Sawtooth, t, t + hit + 0.05, Automation.Constant(f * p),
                    SynthBus.Env(t, 0, (0, 0), (hit * 0.9, 0.3 * v), (hit + 0.05, 0)), det,
                    filterType: FilterType.Lowpass, filterQ: 6, filterFreq: new Automation(150).Set(150, t).ExpTo(1400 * p, t + hit));
            c.Osc(mix, Waveform.Sine, t + 0.1 * L, t + hit, new Automation(760).Set(760 * p, t + 0.1 * L).ExpTo(480 * p, t + hit),
                SynthBus.Env(t + 0.1 * L, 0, (0, 0), (0.25 * L, 0.18 * v), (hit - 0.1 * L, 0.05 * v)), freqLfo: new Lfo(6, 20 * p, t + 0.1 * L));

            double h = t + hit; // 2) 웅!
            c.Blip(d, h, 90 * p, 28, 0.5, 0.9 * v);
            c.Grain(d, h, 0.35, 0.8 * v, FilterType.Lowpass, 900 * p, 1, 0.6, c.Brown);
            c.Osc(mix, Waveform.Sine, h, t + T, new Automation(620).Set(620 * p, h).ExpTo(380 * p, t + T), // 3) 꼬리: 섬뜩한 음이 메아리로 번진다
                SynthBus.Env(h, 0, (0, 0), (0.03, 0.16 * v), (T - hit, 0)), freqLfo: new Lfo(5, 25 * p, h));
            c.Src(mix, c.White, h, T - hit, 0.9, FilterType.Bandpass, 4, Automation.Constant(2600 * p),
                SynthBus.Env(h, 0, (0, 0), (0.05, 0.12 * v), (T - hit, 0)), new Lfo(3, 900, h));
            c.FeedbackDelay(mix, 0.17, 0.45, d);
        }

        // 에스퍼 "염동력": 웅웅웅 맥동(점점 빨라짐) + 공명 필터가 위로 쓸림 + 반짝이는 FM → 공간이 일그러지듯 어긋난 세 음이 떨며 아래로 휘고
        // 웅웅 링 변조 + 휘감기는 바람 + 쿵. (처음 안의 맑은 FM 종소리는 휴대폰 알림음 같다는 의견으로 바꿨다.) 착탄 0.62L.
        static void PsychicTelekinesis(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            double hit = 0.62 * L, T = 1.25 * L;

            double[] mix = c.NewBuffer();
            { // 1) 맥동: 사인 두 개(어긋남) × 진폭 맥동 8 → 16Hz
                var a = new Oscillator(Waveform.Sine, c.SampleRate);
                var b = new Oscillator(Waveform.Sine, c.SampleRate);
                var am = new Oscillator(Waveform.Sine, c.SampleRate);
                var amF = new Automation(8).Set(8, t).ExpTo(16, t + hit);
                var g = SynthBus.Env(t, 0, (0, 0), (hit * 0.9, 0.5 * v), (hit + 0.03, 0));
                int a0 = c.IndexAtOrAfter(t), a1 = c.IndexAtOrAfter(t + hit + 0.03);
                for (int n = a0; n < a1; n++)
                {
                    double tn = c.TimeOf(n);
                    double x = a.Next(220 * p) + 0.7 * b.Next(331 * p);
                    d[n] += x * (0.5 + 0.5 * am.Next(amF.ValueAt(tn))) * g.ValueAt(tn) * 0.6;
                }
            }
            c.Osc(d, Waveform.Sawtooth, t, t + hit, Automation.Constant(110 * p), // 2) 공명 스윕
                SynthBus.Env(t, 0, (0, 0), (hit * 0.8, 0.3 * v), (hit, 0)),
                filterType: FilterType.Bandpass, filterQ: 10, filterFreq: new Automation(300).Set(300, t).ExpTo(3200 * p, t + hit));
            { // 3) FM 반짝임: 깊이가 커지며 금속성으로
                var car = new Oscillator(Waveform.Sine, c.SampleRate);
                var mod = new Oscillator(Waveform.Sine, c.SampleRate);
                var depth = new Automation(0).Set(0, t).LinearTo(900 * p, t + hit);
                var g = SynthBus.Env(t, 0, (0, 0), (hit, 0.12 * v), (hit + 0.02, 0));
                int a0 = c.IndexAtOrAfter(t), a1 = c.IndexAtOrAfter(t + hit + 0.02);
                for (int n = a0; n < a1; n++)
                {
                    double tn = c.TimeOf(n);
                    mix[n] += car.Next(1320 * p + mod.Next(1320 * 1.5 * p) * depth.ValueAt(tn)) * g.ValueAt(tn);
                }
            }

            double h = t + hit; // 4) 일그러지는 공간: 어긋난 세 음이 떨며 0.42배로 휘어 내려간다
            foreach (var (f, det) in new[] { (720.0, -20.0), (720.0, 20.0), (1080.0, 7.0) })
                c.Osc(mix, Waveform.Sine, h, h + 0.55, new Automation(f).Set(f * p, h).ExpTo(f * 0.42 * p, h + 0.55),
                    new Automation(0).Set(0, h).LinearTo(0.2 * v, h + 0.01).ExpTo(0.001, h + 0.55), det, new Lfo(7, f * 0.03 * p, h));
            { // 웅웅: 사인 520Hz × 느려지는 사인(80 → 15Hz)
                var a = new Oscillator(Waveform.Sine, c.SampleRate);
                var b = new Oscillator(Waveform.Sine, c.SampleRate);
                var bf = new Automation(80).Set(80, h).ExpTo(15, h + 0.45);
                var g = new Automation(0).Set(0, h).LinearTo(0.35 * v, h + 0.01).ExpTo(0.001, h + 0.45);
                int a0 = c.IndexAtOrAfter(h), a1 = c.IndexAtOrAfter(h + 0.45);
                for (int n = a0; n < a1; n++)
                {
                    double tn = c.TimeOf(n);
                    mix[n] += a.Next(520 * p) * b.Next(bf.ValueAt(tn)) * g.ValueAt(tn);
                }
            }
            c.Src(d, c.White, h, 0.4, 0.8, FilterType.Bandpass, 2, // 휘감기는 바람
                new Automation(3200).Set(3200 * p, h).ExpTo(450 * p, h + 0.4),
                SynthBus.Env(h, 0, (0, 0), (0.03, 0.35 * v), (0.4, 0)));
            c.Blip(d, h, 170 * p, 55 * p, 0.3, 0.6 * v);
            c.FeedbackDelay(mix, 0.13, 0.3, d);
        }
    }
}
