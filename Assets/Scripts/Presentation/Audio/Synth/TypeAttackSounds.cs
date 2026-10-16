using System;
using System.Collections.Generic;

namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>속성 공격음 레시피가 있는 속성(로스터에 쓰이는 8종). species.json 의 소문자 이름과 1:1.</summary>
    public enum AttackElement { Electric, Water, Fire, Flying, Grass, Poison, Ghost, Psychic }

    /// <summary>
    /// 속성 기반 공격음 합성기. M0 시제품(Tools/audio/type-attack-sounds-prototype.html v3)을 C# 로 옮긴 뒤,
    /// M3-3 에서 8속성 모두 다시 만들었다(사용자가 속성마다 2안 중 선택, 2026-10-17) — 지금 레시피는 C# 에만 있다.
    /// 고르지 않은 안은 TypeAttackSounds.Alternates.cs 에 보관한다(두 번째 기술용).
    /// <b>기술을 쓸 때(공격 모션) 나는 소리다. 포켓몬 울음소리(등장 소리)가 아니다</b> (사용자 정정, 2026-10-17).
    /// 규칙: 주 속성 = 본체(세기 LOUD), 보조 속성 = 덧칠(세기 0.45×LOUD, 0.06×L 늦게, 길이 0.85×L),
    /// 출력 0.8 → 컴프레서(−12dB, 8:1) → 리미터. 같은 입력이면 같은 샘플을 낸다(시드 고정).
    /// UnityEngine 을 쓰지 않는다. WebGL 에서 실행 중 합성은 느려서 에디터가 미리 굽는다(AudioBaker).
    /// </summary>
    public static partial class TypeAttackSounds
    {
        public const int DefaultSampleRate = 22050;

        delegate void Recipe(SynthBus c, double[] d, double t, double p, double L, double v);

        /// <summary>
        /// 속성 공격음을 mono float[] [-1, 1] 로 렌더한다.
        /// </summary>
        /// <param name="types">species.json 의 types (예: ["fire", "flying"]). 첫째가 주 속성, 둘째가 보조 속성.</param>
        /// <param name="semitones">반음 단위 음높이 오프셋(체급이 클수록 낮게).</param>
        /// <param name="lengthMul">길이 배수.</param>
        /// <exception cref="ArgumentException">레시피가 없는 속성이 있을 때(<see cref="IsSupported"/> 로 미리 확인).</exception>
        public static float[] Render(IReadOnlyList<string> types, double semitones = 0, double lengthMul = 1, int sampleRate = DefaultSampleRate)
        {
            if (types == null || types.Count == 0) throw new ArgumentException("속성이 하나 이상 필요합니다.", nameof(types));
            AttackElement primary = Parse(types[0]);
            AttackElement? secondary = types.Count > 1 ? Parse(types[1]) : (AttackElement?)null;
            return Render(primary, secondary, semitones, lengthMul, sampleRate);
        }

        /// <summary>속성 공격음을 mono float[] [-1, 1] 로 렌더한다. secondary 가 null 이면 단일 속성.</summary>
        public static float[] Render(AttackElement primary, AttackElement? secondary, double semitones = 0, double lengthMul = 1, int sampleRate = DefaultSampleRate)
        {
            double[] d = RenderDry(primary, secondary, semitones, lengthMul, sampleRate);
            // 시제품 설정(−12dB, knee 6, 8:1, attack 1ms). release 는 명세 기본값이 0.25초지만 Chrome 은 적응형으로 더 빨리 풀린다.
            // Chrome OfflineAudioContext 렌더와 RMS 를 비교해(14개 중 13개가 ±0.6dB) 0.08초로 맞췄다.
            Dynamics.Compress(d, sampleRate, releaseSec: 0.08);
            Dynamics.Limit(d, sampleRate);
            return Dynamics.TrimToFloat(d, sampleRate);
        }

        /// <summary>컴프레서·리미터·꼬리 정리 전의 믹스(출력 게인 0.8 까지 적용). 시제품과 비교·검증용.</summary>
        internal static double[] RenderDry(AttackElement primary, AttackElement? secondary, double semitones, double lengthMul, int sampleRate)
        {
            if (!(lengthMul > 0)) throw new ArgumentOutOfRangeException(nameof(lengthMul));
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            double len = lengthMul;
            double p = Math.Pow(2, semitones / 12);
            var bus = new SynthBus(sampleRate, 0.06 * len + 1.4 * len + 1.0); // 가장 긴 레시피(전기 1.2L) + 메아리 꼬리
            double[] d = bus.NewBuffer();
            RecipeOf(primary)(bus, d, 0, p, len, Loud(primary));
            if (secondary.HasValue)
            {
                AttackElement s = secondary.Value;
                RecipeOf(s)(bus, d, 0.06 * len, p, len * 0.85, 0.45 * Loud(s));
            }
            for (int i = 0; i < d.Length; i++) d[i] *= 0.8;
            return d;
        }

        public static bool IsSupported(string type) => TryParse(type, out _);

        /// <summary>
        /// 소리 시작부터 "터지는 순간"까지의 시간(초). 연출은 기술이 발사되는 순간보다 이만큼 먼저 소리를 튼다.
        /// 전기는 자체 충전음(0.22L) 뒤에 낙뢰 크랙이 나고, 나머지는 시작과 동시에 발사음이 난다.
        /// </summary>
        public static double LeadSeconds(AttackElement element, double lengthMul)
            => element == AttackElement.Electric ? 0.22 * lengthMul : 0;

        public static bool TryParse(string type, out AttackElement element)
        {
            switch (type == null ? "" : type.Trim().ToLowerInvariant())
            {
                case "electric": element = AttackElement.Electric; return true;
                case "water": element = AttackElement.Water; return true;
                case "fire": element = AttackElement.Fire; return true;
                case "flying": element = AttackElement.Flying; return true;
                case "grass": element = AttackElement.Grass; return true;
                case "poison": element = AttackElement.Poison; return true;
                case "ghost": element = AttackElement.Ghost; return true;
                case "psychic": element = AttackElement.Psychic; return true;
                default: element = default; return false;
            }
        }

        static AttackElement Parse(string type)
        {
            if (TryParse(type, out AttackElement e)) return e;
            throw new ArgumentException($"속성 공격음 레시피가 없는 속성입니다: '{type}' (지원: electric, water, fire, flying, grass, poison, ghost, psychic)");
        }

        /// <summary>속성별 체감 음량 보정(M3-3 레시피 기준, 렌더 RMS 가 대략 −15 ~ −21 dBFS 로 고르게). 없는 속성은 1.</summary>
        static double Loud(AttackElement e)
        {
            switch (e)
            {
                case AttackElement.Grass: return 2.2;
                case AttackElement.Flying: return 1.6;
                default: return 1.0;
            }
        }

        static Recipe RecipeOf(AttackElement e)
        {
            switch (e)
            {
                case AttackElement.Electric: return Electric;
                case AttackElement.Water: return Water;
                case AttackElement.Fire: return Fire;
                case AttackElement.Flying: return Flying;
                case AttackElement.Grass: return Grass;
                case AttackElement.Poison: return Poison;
                case AttackElement.Ghost: return Ghost;
                default: return Psychic;
            }
        }

        // ===================================================================== 레시피
        // 인자: (버스, 출력, 시작 시각 t, 음높이 배수 p, 길이 배수 L, 세기 v). 난수 호출 순서는 시제품 JS 의 인자 평가 순서와 같다.

        // 전기(낙뢰): 위이잉 충전(스파크가 촘촘해짐) → 콰직! 크랙(갈라지는 잔가지 크랙) → 찌지직 치지직 고역 아크 세 덩어리
        // → 치리리릭 잦아드는 잔떨림 → 우르릉 천둥. 크랙은 소리 시작 0.22L 뒤(LeadSeconds). 사용자 요청으로 고역 지직임을 키웠다.
        static void Electric(SynthBus c, double[] d, double t, double p, double L, double v)
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
            c.Blip(d, s, 95 * p, 32, 0.32, 0.8 * v);
            for (int i = 0; i < 5; i++)
                c.Grain(d, s + (0.02 + 0.025 * i + 0.01 * r.Next()) * L * 1.25, 0.02, (0.8 - 0.13 * i) * v,
                    FilterType.Highpass, 900 + 500 * i, 1, 0.4 + i * 0.27);

            // 3) 찌지직 치지직: 고음 버즈(180 + 361Hz 를 tanh 로 찌그러뜨림)와 고역 노이즈를 4~12ms 조각으로 빠르게 끊는다.
            //    밴드패스가 5200 → 2600Hz 로 내려가며, 세 덩어리(찌지직 / 치지직 / 지직)로 점점 약해진다.
            double[] groups = { 0, 0.13, 0.27 }, sizes = { 0.12, 0.1, 0.08 }, amps = { 1, 0.8, 0.6 };
            var gate = new Automation(0).Set(0, s);
            for (int k = 0; k < groups.Length; k++)
                for (double tt = groups[k] * L; tt < (groups[k] + sizes[k]) * L;)
                {
                    double on = 0.004 + r.Next() * 0.008, off = 0.002 + r.Next() * 0.006;
                    gate.Set((0.55 + 0.45 * r.Next()) * amps[k] * v, s + tt);
                    gate.Set(0, s + tt + on);
                    tt += on + off;
                }
            {
                double A = (groups[2] + sizes[2]) * L;
                var saw = new Oscillator(Waveform.Sawtooth, c.SampleRate);
                var sq = new Oscillator(Waveform.Square, c.SampleRate);
                var bp = new Biquad(FilterType.Bandpass, 1.5, c.SampleRate);
                var nbp = new Biquad(FilterType.Bandpass, 1.2, c.SampleRate);
                var bf = new Automation(5200).Set(5200, s).ExpTo(2600, s + A);
                var white = c.White;
                int a0 = c.IndexAtOrAfter(s), a1 = c.IndexAtOrAfter(s + A + 0.02);
                for (int n = a0; n < a1; n++)
                {
                    double tn = c.TimeOf(n), g = gate.ValueAt(tn);
                    double x = saw.Next(180 * p) + 0.7 * sq.Next(361 * p);
                    double buzz = bp.Process(SynthBus.TanhShape(x, 25), bf.ValueAt(tn) * p);
                    double hiss = nbp.Process(white[(n * 7) % white.Length], 6500);
                    d[n] += (buzz * 1.8 + hiss * 0.9) * g;
                }
            }

            // 4) 치리리릭: 고역 미세 딸깍이 처음엔 5ms 간격으로 촘촘하다가 점점 성겨지며 사라진다.
            {
                double tt = 0.3 * L, gap = 0.005, end = 0.75 * L;
                while (tt < end)
                {
                    double k = (tt - 0.3 * L) / (end - 0.3 * L);
                    c.Grain(d, s + tt, 0.0015 + 0.002 * r.Next(), (0.25 + 0.3 * r.Next()) * v * (1 - 0.8 * k),
                        FilterType.Highpass, 4200 + 3000 * r.Next(), 1, r.Next() * 1.7);
                    gap *= 1.07;
                    tt += gap * (0.7 + 0.6 * r.Next());
                }
            }
            Crackle(c, d, r, s, s + 0.45 * L, 40, 0.4 * v, 1.3);

            double rd = T - ch; // 5) 천둥(고역이 묻히지 않게 낮게)
            c.Src(d, c.Brown, s + 0.02, rd, 0.9, FilterType.Lowpass, 1, Automation.Constant(190 * Math.Sqrt(p)),
                Flutter(r, s + 0.02, rd, 0.045, tt => 0.8 * v * Math.Exp(-3.2 * tt / rd) * Math.Min(1, tt / 0.04), 0.6));
        }

        // 물·불·풀은 M3-3 에서 다시 만들었다(사용자가 2안 중 선택: 물대포, 파이어볼 + 활활, 덩굴 휘감기). 시제품 HTML 에는 없는 C# 전용 레시피다.
        // 공통 구조: 시전 → 몸통 → 착탄. 착탄 시각은 연출에 맞춘다: 물대포 물줄기 0.5초(= 0.62L, L = 0.8), 발사형 구체 0.35초(= 0.44L).

        // 물(물대포): 쏴아 솟구침(로우패스가 열림) → 콸콸 몸통(빠른 난류 + 꿀렁이는 저역 + 거품) → 촤악 부서지는 파도 → 흩어지는 물보라.
        static void Water(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // 불꽃(파이어볼 + 활활): 화악 발사 → 날아가는 불덩이 → 펑 폭발 → 숨 쉬듯 일렁이며 타오르는 꼬리(타닥타닥 + 낮은 펑).
        static void Fire(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // 풀(덩굴 휘감기): 잎사귀 사각 → 덩굴이 휙휙 세 번 감기며 점점 조여드는 삐걱 → 착 감김(나무 똑) → 마지막 끼이익.
        static void Grass(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // ===================================================================== 공통 재료 (M3-3)

        /// <summary>난류 게인: stepSec 마다 env(상대 시각) × (1 − depth + depth·난수) 로 흔들리는 자동화. 물살·불길의 일렁임.</summary>
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

        /// <summary>휙: 밴드패스 노이즈가 f0 → f1 로 쓸리고 60% 지점에서 가장 크다.</summary>
        static void Swish(SynthBus c, double[] d, double t, double dur, double f0, double f1, double peak, double offset, double q = 1.6)
        {
            c.Src(d, c.White, t, dur, offset, FilterType.Bandpass, q,
                new Automation(f0).Set(f0, t).ExpTo(f1, t + dur),
                SynthBus.Env(t, 0, (0, 0), (dur * 0.6, peak), (dur, 0)));
        }

        /// <summary>타닥: 아주 짧은 고역 알갱이를 시간 구간에 무작위로 뿌린다(bias &gt; 1 이면 앞쪽에 몰림).</summary>
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

        /// <summary>버퍼를 tanh 로 찌그러뜨려 더한다(불길의 거친 질감).</summary>
        static void AddGritty(double[] d, double[] tmp, double drive, double mix)
        {
            for (int i = 0; i < d.Length; i++)
                if (tmp[i] != 0) d[i] += SynthBus.TanhShape(tmp[i] * drive, 1.6) * mix;
        }

        // 비행(칼바람): 날카로운 공기 베기 "쉭-쉭"(좁은 대역이 위로 빠르게) → 휘이잉 감기는 회오리(빈 관 울림) → 흩어짐.
        static void Flying(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // 독(오물 폭탄): 뿌직 발사(공명이 내려가는 질척임) → 꿀렁이며 날아감(느린 거품) → 철퍽! → 치이익 녹아드는 산성 거품. 착탄 0.44L.
        static void Poison(SynthBus c, double[] d, double t, double p, double L, double v)
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

        // 고스트(원혼의 울음): "우우~" 하는 유령 목소리(포먼트 필터 톱니, 올라갔다 내려옴) + 속삭임 + 어긋난 종소리가 메아리친다. 착탄 0.62L.
        static void Ghost(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(233);
            double hit = 0.62 * L;

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

        // 에스퍼(사이코 빔): 링 변조(두 사인의 곱)로 일렁이는 공상과학 광선 + 25Hz 떨림 + 피융 광선 세 발 → 착탄 웅 + 메아리. 착탄 0.62L.
        static void Psychic(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            double hit = 0.62 * L, T = 1.2 * L;

            double[] mix = c.NewBuffer();
            { // 1) 링 변조 광선: 600Hz × (420 → 900Hz), 25Hz 떨림
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
