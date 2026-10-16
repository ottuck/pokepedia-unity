using System;

namespace Pokepedia.Presentation.Audio.Synth
{
    // M3-3 2차: 전기·비행·독·고스트·에스퍼 공격음 2안씩(사용자 요청: 물·불·풀 새 버전 정도의 품질로). 들어 보고 고른다.
    // 고른 안은 TypeAttackSounds.cs 로 옮기고 이 파일은 지운다.
    // 착탄 시각(연출 기준, L = 0.8): 전기 낙뢰 = 0.22L(자체 충전 뒤, LeadSeconds), 비행 날개치기(접촉) ≈ 0.12L,
    // 독 오물폭탄(발사형) 0.44L, 고스트 섀도볼·에스퍼 사이코키네시스(효과형, 고리 0.5초) 0.62L.
    public static partial class TypeAttackSounds
    {
        internal static readonly string[] CandidateNames =
            { "electric_a", "electric_b", "flying_a", "flying_b", "poison_a", "poison_b", "ghost_a", "ghost_b", "psychic_a", "psychic_b" };

        /// <summary>후보 레시피를 게임과 같은 체인(출력 0.8 → 컴프레서 → 리미터)으로 렌더한다. render 도구 전용.</summary>
        internal static float[] RenderCandidate(string name, double lengthMul, int sampleRate = DefaultSampleRate)
        {
            switch (name)
            {
                case "electric_a": return RenderRecipe(ElectricA, 1.0, 0, lengthMul, sampleRate);
                case "electric_b": return RenderRecipe(ElectricB, 1.0, 0, lengthMul, sampleRate);
                case "flying_a": return RenderRecipe(FlyingA, 1.6, 0, lengthMul, sampleRate);
                case "flying_b": return RenderRecipe(FlyingB, 1.6, 0, lengthMul, sampleRate);
                case "poison_a": return RenderRecipe(PoisonA, 1.0, 0, lengthMul, sampleRate);
                case "poison_b": return RenderRecipe(PoisonB, 1.0, 0, lengthMul, sampleRate);
                case "ghost_a": return RenderRecipe(GhostA, 1.0, 0, lengthMul, sampleRate);
                case "ghost_b": return RenderRecipe(GhostB, 1.0, 0, lengthMul, sampleRate);
                case "psychic_a": return RenderRecipe(PsychicA, 1.3, 0, lengthMul, sampleRate);
                case "psychic_b": return RenderRecipe(PsychicB, 1.0, 0, lengthMul, sampleRate);
                default: throw new ArgumentException(name);
            }
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

        // ===================================================================== 전기 (크랙 = 0.22L, LeadSeconds 와 같다)

        // 전기 A "낙뢰": 위이잉 충전(스파크가 촘촘해짐) → 콰직! 크랙(갈라지는 잔가지 크랙 5개) → 지지직 아크(필터가 내려감) → 우르릉 천둥.
        static void ElectricA(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(101);
            double ch = 0.22 * L, s = t + ch, T = 1.25 * L;

            c.Osc(d, Waveform.Sawtooth, t, s, // 1) 충전 위이잉
                new Automation(300).Set(300 * p, t).ExpTo(2400 * p, s),
                SynthBus.Env(t, 0, (0, 0), (ch * 0.9, 0.22 * v), (ch, 0)),
                freqLfo: new Lfo(40, 90 * p, t, s),
                filterType: FilterType.Bandpass, filterQ: 2, filterFreq: new Automation(800).Set(800, t).ExpTo(3000, s));
            for (int i = 0; i < 22; i++)
                c.Grain(d, t + ch * Math.Sqrt(r.Next()), 0.002 + r.Next() * 0.004, (0.08 + 0.25 * r.Next()) * v,
                    FilterType.Highpass, 3500 + r.Next() * 3500, 1, r.Next() * 1.7);

            c.Grain(d, s, 0.005, 1.5 * v, FilterType.Highpass, 150, 1, 0.33); // 2) 콰직
            c.Grain(d, s, 0.07, 1.1 * v, FilterType.Bandpass, 2600, 0.6, 0.9);
            c.Blip(d, s, 95 * p, 32, 0.32, 0.85 * v);
            for (int i = 0; i < 5; i++)
                c.Grain(d, s + (0.02 + 0.025 * i + 0.01 * r.Next()) * L * 1.25, 0.02, (0.8 - 0.13 * i) * v,
                    FilterType.Highpass, 900 + 500 * i, 1, 0.4 + i * 0.27);

            double A = 0.45 * L; // 3) 아크: 찌그러진 버즈를 불규칙하게 끊고 밴드패스를 3000 → 900 으로 내린다
            {
                var saw = new Oscillator(Waveform.Sawtooth, c.SampleRate);
                var sq = new Oscillator(Waveform.Square, c.SampleRate);
                var bp = new Biquad(FilterType.Bandpass, 1.2, c.SampleRate);
                var bf = new Automation(3000).Set(3000, s).ExpTo(900, s + A);
                var gate = new Automation(0).Set(0, s);
                for (double tt = 0.015; tt < A;)
                {
                    double on = 0.008 + r.Next() * 0.025, off = 0.003 + r.Next() * 0.015;
                    gate.Set((0.5 + 0.5 * r.Next()) * 0.5 * v * Math.Pow(1 - tt / A, 1.3), s + tt);
                    gate.Set(0, s + tt + on);
                    tt += on + off;
                }
                int a0 = c.IndexAtOrAfter(s), a1 = c.IndexAtOrAfter(s + A + 0.03);
                for (int n = a0; n < a1; n++)
                {
                    double tn = c.TimeOf(n);
                    double x = saw.Next(90 * p) + 0.7 * sq.Next(181 * p);
                    d[n] += bp.Process(SynthBus.TanhShape(x, 25), bf.ValueAt(tn)) * gate.ValueAt(tn) * 1.6;
                }
            }
            Crackle(c, d, r, s, s + A, 35, 0.35 * v, 1.4);

            double rd = T - ch; // 4) 천둥
            c.Src(d, c.Brown, s + 0.02, rd, 0.9, FilterType.Lowpass, 1, Automation.Constant(190 * Math.Sqrt(p)),
                Flutter(r, s + 0.02, rd, 0.045, tt => 1.2 * v * Math.Exp(-3.2 * tt / rd) * Math.Min(1, tt / 0.04), 0.6));
        }

        // 전기 B "방전": 우웅 고압 험(찌그러진 120Hz) 충전 → 파지직! 혼돈의 아크(끊길 때마다 음색이 바뀜) + 지직 방전 3줄기 → 탁 끊기고 오존 쉬이.
        static void ElectricB(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // ===================================================================== 비행 (날개치기 = 접촉, 맞는 순간 ≈ 0.12L)

        // 비행 A "날개 강타": 묵직한 펄럭(저역 쿵 + 바람) → 휘익 내려치는 바람 → 소용돌이치며 흩어지는 돌풍.
        static void FlyingA(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // 비행 B "칼바람": 날카로운 공기 베기 "쉭-쉭"(좁은 대역이 위로 빠르게) → 휘이잉 감기는 회오리(빈 관 울림) → 흩어짐.
        static void FlyingB(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(171);
            double T = 1.0 * L;

            Swish(c, d, t, 0.07, 1800 * p, 8500 * p, 0.8 * v, 0.2, 4); // 1) 쉭 쉭
            Swish(c, d, t + 0.1 * L, 0.07, 2200 * p, 9500 * p, 0.85 * v, 0.9, 4);
            c.Grain(d, t + 0.07, 0.01, 0.4 * v, FilterType.Highpass, 5000, 1, 0.3);
            c.Grain(d, t + 0.1 * L + 0.07, 0.01, 0.45 * v, FilterType.Highpass, 5500, 1, 1.0);

            double w0 = t + 0.12 * L, wd = T - 0.12 * L; // 2) 휘이잉 회오리: 공명 대역이 4.5Hz 로 감긴다 + 빈 관 울림
            c.Src(d, c.White, w0, wd, 1.4, FilterType.Bandpass, 3,
                new Automation(1500 * p).Set(1500 * p, w0).ExpTo(2600 * p, w0 + wd * 0.5).ExpTo(1100 * p, w0 + wd),
                Flutter(r, w0, wd, 0.04, tt => 0.5 * v * Math.Min(1, tt / 0.08) * Math.Max(0, 1 - tt / wd), 0.3),
                new Lfo(4.5, 700 * p, w0));
            c.Src(d, c.White, w0, wd, 0.6, FilterType.Bandpass, 9, Automation.Constant(620 * p),
                SynthBus.Env(w0, 0, (0, 0), (0.15 * L, 0.35 * v), (wd, 0)));
            c.Src(d, c.Brown, w0, wd * 0.7, 1.7, FilterType.Lowpass, 1, Automation.Constant(300 * p),
                SynthBus.Env(w0, 0, (0, 0.4 * v), (wd * 0.7, 0)));
        }

        // ===================================================================== 독 (오물폭탄 = 발사형, 착탄 0.44L)

        // 독 A "오물 폭탄": 뿌직 발사(공명이 내려가는 질척임) → 꿀렁이며 날아감(느린 거품) → 철퍽! → 치이익 녹아드는 산성 거품.
        static void PoisonA(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(191);
            double hit = 0.44 * L, T = 1.2 * L;

            c.Src(d, c.Brown, t, 0.12, 0.2, FilterType.Lowpass, 12, // 1) 뿌직: 공명 로우패스가 1600 → 250 으로
                new Automation(1600).Set(1600 * p, t).ExpTo(250 * p, t + 0.11),
                SynthBus.Env(t, 0, (0, 0), (0.01, 0.9 * v), (0.12, 0)));
            c.Blip(d, t, 240 * p, 90 * p, 0.12, 0.45 * v);

            c.Osc(d, Waveform.Sine, t + 0.05, t + hit, Automation.Constant(85 * p), // 2) 꿀렁
                SynthBus.Env(t + 0.05, 0, (0, 0), (0.05, 0.4 * v), (hit - 0.05, 0.3 * v)), freqLfo: new Lfo(9, 25 * p, t + 0.05));
            for (int i = 0; i < 14; i++)
                Bubble(c, d, t + 0.05 + r.Next() * (hit - 0.08), (130 + 260 * r.Next()) * p, 1.3 + 0.4 * r.Next(), 0.05 + 0.05 * r.Next(), (0.12 + 0.12 * r.Next()) * v);

            double h = t + hit; // 3) 철퍽
            c.Src(d, c.Brown, h, 0.25, 0.7, FilterType.Lowpass, 6,
                new Automation(2200).Set(2200 * p, h).ExpTo(400 * p, h + 0.2),
                SynthBus.Env(h, 0, (0, 0), (0.006, 1.1 * v), (0.25, 0)));
            c.Grain(d, h, 0.03, 0.6 * v, FilterType.Highpass, 1800, 1, 1.2);
            c.Blip(d, h, 120 * p, 40 * p, 0.18, 0.6 * v);

            double z0 = h + 0.05, zd = T - hit - 0.05; // 4) 치이익 + 산성 거품
            c.Src(d, c.White, z0, zd, 1.5, FilterType.Highpass, 1, Automation.Constant(3800),
                Flutter(r, z0, zd, 0.02, tt => 0.32 * v * Math.Max(0, 1 - tt / zd), 0.7));
            Crackle(c, d, r, z0, z0 + zd * 0.8, 40, 0.25 * v, 1.3);
            for (int i = 0; i < 18; i++)
                Bubble(c, d, z0 + Math.Pow(r.Next(), 1.4) * zd * 0.85, (220 + 500 * r.Next()) * p, 1.4 + 0.4 * r.Next(), 0.03 + 0.04 * r.Next(), (0.07 + 0.1 * r.Next()) * v);
        }

        // 독 B "독 늪": 부글부글 끓는 걸쭉한 거품 + 메스꺼운 어긋난 두 음(트레몰로) + 점점 커지는 치이익 → 꿀렁! 큰 거품이 터짐.
        static void PoisonB(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // ===================================================================== 고스트 (섀도볼 = 효과형, 착탄 0.62L)

        // 고스트 A "섀도볼": 어긋난 저음 톱니가 우우웅 차오름(필터가 열림) + 위에서 떨며 내려오는 섬뜩한 음 → 웅! 어둠이 터짐 → 메아리.
        static void GhostA(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // 고스트 B "원혼의 울음": "우우~" 하는 유령 목소리(포먼트 필터 톱니, 올라갔다 내려옴) + 속삭임 + 어긋난 종소리가 메아리친다.
        static void GhostB(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(233);
            double hit = 0.62 * L, T = 1.3 * L;

            double[] mix = c.NewBuffer();
            { // 1) 유령 목소리: 220 → 440 → 260Hz, 5Hz 떨림, 포먼트 "우"(350, 800) → "오"(500, 1000)
                var osc = new Oscillator(Waveform.Sawtooth, c.SampleRate);
                var f1 = new Biquad(FilterType.Bandpass, 6, c.SampleRate);
                var f2 = new Biquad(FilterType.Bandpass, 8, c.SampleRate);
                var pitch = new Automation(220).Set(220 * p, t).ExpTo(440 * p, t + 0.35 * L).ExpTo(260 * p, t + 0.95 * L);
                var vib = new Lfo(5, 1, t + 0.05);
                var fa = new Automation(350).Set(350, t).LinearTo(500, t + 0.95 * L);
                var fb = new Automation(800).Set(800, t).LinearTo(1000, t + 0.95 * L);
                var g = SynthBus.Env(t, 0, (0, 0), (0.15 * L, 0.55 * v), (0.7 * L, 0.45 * v), (0.95 * L, 0));
                int a0 = c.IndexAtOrAfter(t), a1 = c.IndexAtOrAfter(t + 0.95 * L);
                for (int n = a0; n < a1; n++)
                {
                    double tn = c.TimeOf(n);
                    double x = osc.Next(pitch.ValueAt(tn) * (1 + 0.025 * vib.ValueAt(tn)));
                    mix[n] += (f1.Process(x, fa.ValueAt(tn) * p) + 0.6 * f2.Process(x, fb.ValueAt(tn) * p)) * g.ValueAt(tn);
                }
            }
            c.Src(mix, c.White, t + 0.05, 0.6 * L, 0.4, FilterType.Bandpass, 3, // 2) 속삭임
                new Automation(3000).Set(3000, t),
                Flutter(r, t + 0.05, 0.6 * L, 0.03, tt => 0.18 * v, 0.9), new Lfo(2.3, 1500, t));

            double h = t + hit; // 3) 어긋난 종(비조화 배음 1, 2.76, 5.4)
            double[] parts = { 1, 2.76, 5.4 };
            for (int i = 0; i < parts.Length; i++)
                c.Blip(mix, h, 330 * parts[i] * p, 330 * parts[i] * p * 0.985, 0.6 - 0.15 * i, (0.3 - 0.08 * i) * v);
            c.Blip(d, h, 110 * p, 40 * p, 0.3, 0.6 * v);
            c.FeedbackDelay(mix, 0.19, 0.42, d);
        }

        // ===================================================================== 에스퍼 (사이코키네시스 = 효과형, 착탄 0.62L)

        // 에스퍼 A "염동력": 웅웅웅 맥동(점점 빨라짐) + 공명 필터가 위로 쓸림 + 반짝이는 FM → 띵! 밝은 종 폭발 + 메아리.
        static void PsychicA(SynthBus c, double[] d, double t, double p, double L, double v)
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

            double h = t + hit; // 4) 띵! FM 종(변조 깊이가 빠르게 줄어 맑아짐)
            {
                var car = new Oscillator(Waveform.Sine, c.SampleRate);
                var mod = new Oscillator(Waveform.Sine, c.SampleRate);
                var depth = new Automation(1800).Set(1800 * p, h).ExpTo(30, h + 0.5);
                var g = new Automation(0).Set(0, h).LinearTo(0.55 * v, h + 0.003).ExpTo(0.001, t + T);
                int a0 = c.IndexAtOrAfter(h), a1 = c.IndexAtOrAfter(t + T);
                for (int n = a0; n < a1; n++)
                {
                    double tn = c.TimeOf(n);
                    mix[n] += car.Next(880 * p + mod.Next(880 * 1.4 * p) * depth.ValueAt(tn)) * g.ValueAt(tn);
                }
            }
            c.Blip(d, h, 160 * p, 60 * p, 0.25, 0.5 * v);
            c.FeedbackDelay(mix, 0.13, 0.4, d);
        }

        // 에스퍼 B "사이코 빔": 링 변조(두 사인의 곱)로 일렁이는 공상과학 광선 + 25Hz 떨림 + 피융 광선 세 발 → 착탄 웅 + 메아리.
        static void PsychicB(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            double hit = 0.62 * L, T = 1.2 * L;

            double[] mix = c.NewBuffer();
            { // 1) 링 변조 광선: 600Hz × (420 → 900Hz) , 25Hz 떨림
                var a = new Oscillator(Waveform.Sine, c.SampleRate);
                var b = new Oscillator(Waveform.Sine, c.SampleRate);
                var fb = new Automation(420).Set(420 * p, t).ExpTo(900 * p, t + hit);
                var vib = new Lfo(25, 1, t);
                var g = SynthBus.Env(t, 0, (0, 0), (0.06 * L, 0.45 * v), (hit, 0.4 * v), (hit + 0.15, 0));
                int a0 = c.IndexAtOrAfter(t), a1 = c.IndexAtOrAfter(t + hit + 0.15);
                for (int n = a0; n < a1; n++)
                {
                    double tn = c.TimeOf(n);
                    double w = 1 + 0.03 * vib.ValueAt(tn);
                    mix[n] += a.Next(600 * p * w) * b.Next(fb.ValueAt(tn) * w) * g.ValueAt(tn);
                }
            }
            foreach (double det in new[] { -12.0, 0.0, 12.0 }) // 2) 위상이 어긋나 일렁이는 화음(코러스)
                c.Osc(mix, Waveform.Triangle, t, t + hit, Automation.Constant(440 * p),
                    SynthBus.Env(t, 0, (0, 0), (0.2 * L, 0.08 * v), (hit, 0)), det);
            double[] zaps = { 0.05, 0.22, 0.4 }; // 3) 피융
            foreach (double z in zaps)
                c.Blip(mix, t + z * L, 2600 * p, 320 * p, 0.09, 0.35 * v, Waveform.Sine);

            double h = t + hit; // 4) 착탄
            c.Blip(d, h, 200 * p, 55 * p, 0.3, 0.6 * v);
            c.Osc(mix, Waveform.Sine, h, t + T, new Automation(1200).Set(1200 * p, h).ExpTo(500 * p, t + T),
                SynthBus.Env(h, 0, (0, 0.3 * v), (T - hit, 0)), freqLfo: new Lfo(12, 40 * p, h));
            c.FeedbackDelay(mix, 0.11, 0.45, d);
        }
    }
}
