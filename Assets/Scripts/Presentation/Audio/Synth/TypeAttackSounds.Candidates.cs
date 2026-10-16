using System;

namespace Pokepedia.Presentation.Audio.Synth
{
    // M3-3 물·불·풀 공격음 다시 만들기(사용자 요청: 다른 게임의 속성 마법 소리처럼). 각 2안(A/B)을 들어 보고 고른다.
    // 고른 안은 TypeAttackSounds.cs 의 Water/Fire/Grass 로 옮기고 이 파일은 지운다.
    // 레퍼런스 공통 구조: 시전(cast) → 몸통(loop/travel) → 착탄(impact) 레이어를 따로 만든다.
    // 착탄 시각은 연출에 맞춘다: 물대포 물줄기 0.5초(= 0.62L, L = 0.8), 발사형 구체 0.35초(= 0.44L).
    public static partial class TypeAttackSounds
    {
        internal static readonly string[] CandidateNames = { "water_a", "water_b", "fire_a", "fire_b", "grass_a", "grass_b" };

        /// <summary>후보 레시피를 게임과 같은 체인(출력 0.8 → 컴프레서 → 리미터)으로 렌더한다. render 도구 전용.</summary>
        internal static float[] RenderCandidate(string name, double lengthMul, int sampleRate = DefaultSampleRate)
        {
            Recipe recipe;
            double loud;
            switch (name)
            {
                case "water_a": recipe = WaterA; loud = 1.4; break;
                case "water_b": recipe = WaterB; loud = 1.0; break;
                case "fire_a": recipe = FireA; loud = 1.0; break;
                case "fire_b": recipe = FireB; loud = 1.0; break;
                case "grass_a": recipe = GrassA; loud = 2.2; break;
                case "grass_b": recipe = GrassB; loud = 1.8; break;
                default: throw new ArgumentException(name);
            }
            double len = lengthMul;
            var bus = new SynthBus(sampleRate, 1.6 * len + 0.6);
            double[] d = bus.NewBuffer();
            recipe(bus, d, 0, 1, len, loud);
            for (int i = 0; i < d.Length; i++) d[i] *= 0.8;
            Dynamics.Compress(d, sampleRate, releaseSec: 0.08);
            Dynamics.Limit(d, sampleRate);
            return Dynamics.TrimToFloat(d, sampleRate);
        }

        // ===================================================================== 공통 재료

        /// <summary>
        /// 난류 게인: stepSec 마다 env(상대 시각) × (1 − depth + depth·난수) 로 흔들리는 자동화. 물살·불길의 일렁임.
        /// </summary>
        static Automation Flutter(Lcg r, double t, double dur, double stepSec, Func<double, double> env, double depth)
        {
            var a = new Automation(0).Set(0, t);
            for (double tt = 0; tt < dur; tt += stepSec)
                a.LinearTo(env(tt) * (1 - depth + depth * r.Next()), t + tt);
            a.LinearTo(0, t + dur);
            return a;
        }

        /// <summary>거품 하나: 위로 미끄러지는 사인(거품이 오르며 공명이 올라가는 소리) + 빠른 감쇠.</summary>
        static void Bubble(SynthBus c, double[] d, double t, double f0, double rise, double dur, double peak)
            => c.Blip(d, t, f0, f0 * rise, dur, peak);

        /// <summary>
        /// 나무 삐걱임(스틱-슬립): 아주 좁은 펄스열(rate Hz, 10ms 마다 ±jitter 로 흔들림)을 공명 밴드패스 3개에 통과시킨다.
        /// rate 가 오르면 "끼이익" 하고 조여드는 느낌, 내려가면 "끄으윽" 신음.
        /// </summary>
        static void Creak(SynthBus c, double[] d, Lcg r, double start, double stop, Automation rate, Automation gain,
            double r1, double r2, double r3, double jitter = 0.18)
        {
            var osc = new Oscillator(Waveform.Pulse, c.SampleRate, 0.02);
            var b1 = new Biquad(FilterType.Bandpass, 9, c.SampleRate);
            var b2 = new Biquad(FilterType.Bandpass, 11, c.SampleRate);
            var b3 = new Biquad(FilterType.Bandpass, 13, c.SampleRate);
            int a = c.IndexAtOrAfter(start), b = c.IndexAtOrAfter(stop);
            int hop = Math.Max(1, c.SampleRate / 100);
            double jit = 1;
            for (int n = a; n < b; n++)
            {
                if ((n - a) % hop == 0) jit = 1 + jitter * (2 * r.Next() - 1);
                double t = c.TimeOf(n);
                double x = osc.Next(rate.ValueAt(t) * jit);
                double y = b1.Process(x, r1) * 1.0 + b2.Process(x, r2) * 0.7 + b3.Process(x, r3) * 0.45;
                d[n] += y * gain.ValueAt(t);
            }
        }

        /// <summary>휙: 밴드패스 노이즈가 f0 → f1 로 쓸리고 가운데서 가장 크다.</summary>
        static void Swish(SynthBus c, double[] d, double t, double dur, double f0, double f1, double peak, double offset, double q = 1.6)
        {
            c.Src(d, c.White, t, dur, offset, FilterType.Bandpass, q,
                new Automation(f0).Set(f0, t).ExpTo(f1, t + dur),
                SynthBus.Env(t, 0, (0, 0), (dur * 0.6, peak), (dur, 0)));
        }

        /// <summary>타닥: 아주 짧은 고역 알갱이를 시간 구간에 무작위로 뿌린다(뒤로 갈수록 성기게 하려면 bias &gt; 1).</summary>
        static void Crackle(SynthBus c, double[] d, Lcg r, double t0, double t1, int count, double peak, double bias = 1)
        {
            for (int i = 0; i < count; i++)
            {
                double gt = t0 + Math.Pow(r.Next(), bias) * (t1 - t0);
                double dur = 0.001 + r.Next() * 0.004;
                double pk = (0.3 + 0.7 * r.Next()) * peak;
                double f = 1800 + r.Next() * 4500;
                c.Grain(d, gt, dur, pk, FilterType.Highpass, f, 1, r.Next() * 1.7);
            }
        }

        /// <summary>노이즈를 tanh 로 찌그러뜨려 더한다(불길의 거친 질감).</summary>
        static void AddGritty(double[] d, double[] tmp, double drive, double mix)
        {
            for (int i = 0; i < d.Length; i++)
                if (tmp[i] != 0) d[i] += SynthBus.TanhShape(tmp[i] * drive, 1.6) * mix;
        }

        // ===================================================================== 물

        // 물 A "물 마법": 거품이 차오르며 음이 올라감 → 물줄기 쏴아(난류) → 착탄 첨벙 + 거품 폭발 → 물방울 똑똑. 위에 옅은 마법 반짝임.
        static void WaterA(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // 물 B "물대포": 쏴아 솟구침(로우패스가 열림) → 콸콸 몸통(빠른 난류 + 꿀렁이는 저역 + 거품) → 촤악 부서지는 파도 → 흩어지는 물보라.
        static void WaterB(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(77);
            double hit = 0.62 * L, T = 1.1 * L;

            c.Src(d, c.White, t, 0.2 * L, 0.2, FilterType.Lowpass, 2, // 1) 솟구침
                new Automation(300).Set(300, t).ExpTo(3200 * p, t + 0.16 * L),
                SynthBus.Env(t, 0, (0, 0), (0.12 * L, 0.7 * v), (0.2 * L, 0.35 * v)));

            double s0 = t + 0.08 * L, sd = hit - 0.08 * L + 0.04; // 2) 콸콸 몸통
            c.Src(d, c.White, s0, sd, 0.7, FilterType.Bandpass, 0.9, Automation.Constant(1300 * p),
                Flutter(r, s0, sd, 0.008, tt => 0.6 * v, 0.75));
            c.Src(d, c.Brown, s0, sd, 1.1, FilterType.Lowpass, 3, Automation.Constant(380 * p),
                Flutter(r, s0, sd, 0.04, tt => 0.75 * v, 0.6));
            for (int i = 0; i < 30; i++)
                Bubble(c, d, s0 + r.Next() * sd, (380 + 1000 * r.Next()) * p, 1.5 + 0.6 * r.Next(), 0.02 + 0.04 * r.Next(), (0.05 + 0.1 * r.Next()) * v);

            double h = t + hit; // 3) 촤악
            c.Src(d, c.White, h, 0.42, 1.4, FilterType.Lowpass, 1,
                new Automation(7000).Set(7000, h).ExpTo(700, h + 0.4),
                SynthBus.Env(h, 0, (0, 0), (0.008, 1.0 * v), (0.42, 0)));
            c.Grain(d, h, 0.1, 0.5 * v, FilterType.Highpass, 2500, 1, 0.6);
            c.Blip(d, h, 130 * p, 45 * p, 0.2, 0.6 * v);

            c.Src(d, c.White, h + 0.08, T - hit - 0.08, 1.6, FilterType.Highpass, 1, Automation.Constant(3200), // 4) 물보라
                SynthBus.Env(h + 0.08, 0, (0, 0.28 * v), (T - hit - 0.08, 0)));
            for (int i = 0; i < 12; i++)
                Bubble(c, d, h + 0.08 + r.Next() * (T - hit - 0.1), (1200 + 1800 * r.Next()) * p, 2.2, 0.025, (0.04 + 0.08 * r.Next()) * v);
        }

        // ===================================================================== 불꽃

        // 불 A "화염 방사": 점화 후웅 → 거칠게 타오르는 굉음이 커짐(쉬익 + 타닥) → 콰앙 → 계속 활활 타다가 잦아듦.
        static void FireA(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // 불 B "파이어볼 + 활활": 화악 발사 → 날아가는 불덩이 → 펑 폭발 → 숨 쉬듯 일렁이며 타오르는 꼬리(타닥타닥 + 낮은 펑).
        static void FireB(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(29);
            double hit = 0.44 * L, T = 1.3 * L;

            c.Src(d, c.White, t, hit, 0.15, FilterType.Lowpass, 1.5, // 1) 화악
                new Automation(250).Set(250, t).ExpTo(5000 * p, t + 0.08 * L).ExpTo(1200 * p, t + hit),
                SynthBus.Env(t, 0, (0, 0), (0.05 * L, 0.55 * v), (hit, 0.15 * v)));

            double[] ball = c.NewBuffer(); // 2) 날아가는 불덩이
            c.Src(ball, c.Brown, t + 0.02, hit, 0.4, FilterType.Lowpass, 2, Automation.Constant(1100 * p),
                Flutter(r, t + 0.02, hit, 0.018, tt => 0.7 * v, 0.5));
            AddGritty(d, ball, 2.0, 0.5);
            Crackle(c, d, r, t + 0.03, t + hit, 18, 0.3 * v);

            double h = t + hit; // 3) 펑
            c.Blip(d, h, 170 * p, 30, 0.42, 0.85 * v);
            c.Grain(d, h, 0.5, 1.1 * v, FilterType.Lowpass, 1300 * p, 1, 1.4, c.Brown);
            c.Grain(d, h, 0.22, 0.45 * v, FilterType.Bandpass, 2200, 0.7, 0.3);
            Crackle(c, d, r, h, h + 0.15, 20, 0.55 * v, 1.5);

            double[] burn = c.NewBuffer(); // 4) 활활: 두 번 숨 쉬듯 부풀며 잦아듦
            double bd = T - hit;
            c.Src(burn, c.Brown, h + 0.05, bd, 0.9, FilterType.Lowpass, 1, Automation.Constant(800 * p),
                Flutter(r, h + 0.05, bd, 0.035, tt => 0.75 * v * (1 - tt / bd) * (0.65 + 0.35 * Math.Sin(2 * Math.PI * 2.6 * tt)), 0.45));
            AddGritty(d, burn, 2.0, 0.5);
            Crackle(c, d, r, h + 0.1, t + T, 45, 0.45 * v, 1.2);
            for (int i = 0; i < 4; i++) // 낮은 "펑"(장작이 터짐)
                c.Grain(d, h + 0.12 + r.Next() * (bd - 0.2), 0.05, (0.25 + 0.2 * r.Next()) * v, FilterType.Bandpass, 700 + 400 * r.Next(), 2, r.Next());
        }

        // ===================================================================== 풀

        // 풀 A "덩굴 휘감기": 잎사귀 사각 → 덩굴이 휙휙 세 번 감기며 점점 조여드는 삐걱 → 착 감김(나무 똑) → 마지막 끼이익.
        static void GrassA(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(57);
            double wrap = 0.44 * L, T = 1.0 * L;

            for (int i = 0; i < 40; i++) // 1) 잎사귀
                c.Grain(d, t + r.Next() * 0.35 * L, 0.006 + 0.016 * r.Next(), (0.05 + 0.12 * r.Next()) * v,
                    FilterType.Bandpass, (3200 + 4500 * r.Next()), 1.5, r.Next() * 1.7);

            double[] whips = { 0.04, 0.16, 0.28 }; // 2) 휙 휙 휙 (감길수록 높아짐)
            for (int i = 0; i < whips.Length; i++)
                Swish(c, d, t + whips[i] * L, 0.13 * L, 450 * Math.Pow(1.2, i) * p, 3800 * Math.Pow(1.15, i) * p, (0.38 + 0.08 * i) * v, 0.3 + i * 0.4);

            Creak(c, d, r, t + 0.08 * L, t + 0.78 * L, // 3) 조여드는 삐걱: 빨라지며 커짐
                new Automation(22).Set(22, t + 0.08 * L).ExpTo(95, t + wrap).ExpTo(70, t + 0.78 * L),
                SynthBus.Env(t + 0.08 * L, 0, (0, 0), (wrap - 0.08 * L, 0.55 * v), (0.55 * L, 0.45 * v), (0.7 * L, 0)),
                340 * p, 900 * p, 1750 * p);

            double h = t + wrap; // 4) 착 + 나무 똑
            c.Grain(d, h, 0.012, 0.6 * v, FilterType.Highpass, 2200, 1, 0.2);
            c.Grain(d, h, 0.1, 1.6 * v, FilterType.Bandpass, 1000 * p, 12, 0.6);
            c.Blip(d, h, 520 * p, 470 * p, 0.09, 0.3 * v, Waveform.Sine);
            c.Blip(d, h, 1380 * p, 1300 * p, 0.05, 0.15 * v, Waveform.Sine);
            c.Blip(d, h, 140 * p, 60 * p, 0.12, 0.35 * v, Waveform.Sine);

            Creak(c, d, r, t + 0.62 * L, t + T, // 5) 끼이익(꽉 조임)
                new Automation(120).Set(120, t + 0.62 * L).ExpTo(55, t + T),
                SynthBus.Env(t + 0.62 * L, 0, (0, 0), (0.06 * L, 0.45 * v), (T - 0.62 * L, 0)),
                480 * p, 1250 * p, 2400 * p, 0.1);
        }

        // 풀 B "뿌리 솟구침": 땅이 우지끈 갈라짐(저역 울림 + 쪼개짐) → 굵은 뿌리가 끄으윽 뻗음 → 휙휙 휘감아 → 꽉 조이는 끼익 → 잎사귀 흩날림.
        static void GrassB(SynthBus c, double[] d, double t, double p, double L, double v)
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
