using System;
using System.Collections.Generic;

namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>속성 공격음 레시피가 있는 속성(로스터에 쓰이는 8종). species.json 의 소문자 이름과 1:1.</summary>
    public enum AttackElement { Electric, Water, Fire, Flying, Grass, Poison, Ghost, Psychic }

    /// <summary>
    /// 속성 기반 공격음 합성기 (Tools/audio/type-attack-sounds-prototype.html v3 레시피의 오프라인 C# 이식).
    /// <b>기술을 쓸 때(공격 모션) 나는 소리다. 포켓몬 울음소리(등장 소리)가 아니다</b> (사용자 정정, 2026-10-17).
    /// 규칙: 주 속성 = 본체(세기 LOUD), 보조 속성 = 덧칠(세기 0.45×LOUD, 0.06×L 늦게, 길이 0.85×L),
    /// 출력 0.8 → 컴프레서(−12dB, 8:1) → 리미터. 같은 입력이면 같은 샘플을 낸다(시드 고정).
    /// UnityEngine 을 쓰지 않는다. WebGL 에서 실행 중 합성은 느려서 에디터가 미리 굽는다(AudioBaker).
    /// </summary>
    public static class TypeAttackSounds
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

        /// <summary>속성별 체감 음량 보정 (시제품 LOUD 표, 없는 속성은 1).</summary>
        static double Loud(AttackElement e)
        {
            switch (e)
            {
                case AttackElement.Water: return 2.2;
                case AttackElement.Grass: return 2.0;
                case AttackElement.Fire: return 1.3;
                case AttackElement.Poison: return 2.0;
                case AttackElement.Flying: return 2.5;
                default: return 1.0; // electric 1.0, ghost·psychic 은 표에 없음
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

        // 전기(라이트닝): 충전 "위이잉" → 낙뢰 크랙 → 전류 아크 "지지직" → 천둥 꼬리 "우르릉".
        static void Electric(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(25);
            double ch = 0.22 * L, T = 1.1 * L, s = t + ch;

            // 1) 충전: 올라가는 톱니 + 28Hz 떨림 → 밴드패스, 스파크가 점점 촘촘해짐
            c.Osc(d, Waveform.Sawtooth, t, s + 0.02,
                new Automation(440).Set(200 * p, t).ExpTo(1700 * p, s),
                SynthBus.Env(t, 0, (0, 0), (ch * 0.9, 0.14 * v), (ch, 0)),
                freqLfo: new Lfo(28, 70 * p, t, s + 0.02),
                filterType: FilterType.Bandpass, filterQ: 1.1, filterFreq: Automation.Constant(1400 * p));
            for (int i = 0; i < 18; i++)
            {
                double gt = t + ch * Math.Sqrt(r.Next());
                double dur = 0.002 + r.Next() * 0.005;
                double pk = (0.06 + 0.2 * r.Next()) * v;
                double f = 4000 + r.Next() * 3000;
                double off = r.Next() * 2;
                c.Grain(d, gt, dur, pk, FilterType.Highpass, f, 1, off);
            }

            // 2) 낙뢰 크랙
            c.Grain(d, s, 0.07, 1.3 * v, FilterType.Highpass, 400, 1, 0.33);
            c.Grain(d, s, 0.05, 1.0 * v, FilterType.Lowpass, 3500, 1, 1.2, c.Brown);
            double[] cracks = { 0.03, 0.065, 0.11, 0.16 };
            for (int i = 0; i < cracks.Length; i++)
                c.Grain(d, s + cracks[i] * L, 0.025 + 0.01 * i, (0.75 - 0.15 * i) * v, FilterType.Highpass, 700 + i * 500, 1, 0.5 + i * 0.3);

            // 3) 전류 아크: tanh(30) 로 찌그러뜨린 저음 버즈를 10~35ms 켜짐 / 3~20ms 꺼짐으로 불규칙하게 끊음
            double A = 0.45 * L;
            var gate = new Automation(0).Set(0, t);
            for (double tt = 0.02; tt < A;)
            {
                double on = 0.01 + r.Next() * 0.025;
                double offT = 0.003 + r.Next() * 0.017;
                double a = (0.4 + 0.6 * r.Next()) * 0.35 * v * Math.Pow(1 - tt / A, 1.5);
                gate.Set(a, s + tt);
                gate.Set(0, s + tt + on);
                tt += on + offT;
            }
            {
                double stop = s + A + 0.05;
                var saw = new Oscillator(Waveform.Sawtooth, c.SampleRate);
                var sq = new Oscillator(Waveform.Square, c.SampleRate);
                var lfo = new Lfo(53, 30 * p, s, stop);
                var hp = new Biquad(FilterType.Highpass, 1, c.SampleRate);
                double det20 = Math.Pow(2, 20.0 / 1200);
                int a0 = c.IndexAtOrAfter(s), a1 = c.IndexAtOrAfter(stop);
                for (int n = a0; n < a1; n++)
                {
                    double tn = c.TimeOf(n);
                    double l = lfo.ValueAt(tn);
                    double x = saw.Next(85 * p + l) + sq.Next((172 * p + l) * det20);
                    double y = hp.Process(SynthBus.TanhShape(x, 30), 900);
                    d[n] += y * gate.ValueAt(tn);
                }
            }
            for (int i = 0; i < 30; i++)
            {
                double gt = s + Math.Pow(r.Next(), 1.6) * A;
                double dur = 0.002 + r.Next() * 0.006;
                double pk = (0.15 + 0.35 * r.Next()) * v;
                double f = 2500 + r.Next() * 5000;
                double off = r.Next() * 2;
                c.Grain(d, gt, dur, pk, FilterType.Highpass, f, 1, off);
            }

            // 4) 천둥 꼬리: 저역 브라운 노이즈가 45ms 마다 일렁이며 지수적으로 사라짐
            var fl = new Automation(0).Set(0, s);
            for (double tt = 0.01; tt < T - ch; tt += 0.045)
                fl.LinearTo((0.35 + 0.65 * r.Next()) * 1.1 * v * Math.Exp(-3 * tt / (T - ch)), s + tt);
            fl.LinearTo(0, s + T - ch + 0.05);
            c.Src(d, c.Brown, s, T - ch + 0.1, 0.9, FilterType.Lowpass, 0.7, Automation.Constant(240 * Math.Sqrt(p)), fl);
        }

        // 물: 출렁이는 물살(밴드패스 노이즈 + 6Hz LFO) + 거품 "뽀글" + 첨벙 + 마법 반짝임(삼각파 아르페지오와 메아리).
        static void Water(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(9);
            double T = 0.95 * L;
            c.Src(d, c.White, t, T, 0.3, FilterType.Bandpass, 2.2,
                SynthBus.Env(t, 500 * p, (0, 300 * p), (0.35 * L, 1300 * p), (0.95 * L, 450 * p)),
                SynthBus.Env(t, 0, (0, 0), (0.25 * L, 0.5 * v), (0.6 * L, 0.35 * v), (T, 0)),
                new Lfo(6, 180 * p, t, t + T + 0.05));
            for (int i = 0; i < 18; i++) // 거품
            {
                double f0 = (450 + r.Next() * 900) * p;
                double dur = 0.025 + r.Next() * 0.05;
                double bt = t + 0.04 + r.Next() * (T - 0.15);
                double f1 = f0 * (1.7 + r.Next() * 0.8);
                double pk = (0.08 + 0.12 * r.Next()) * v;
                c.Blip(d, bt, f0, f1, dur, pk);
            }
            c.Grain(d, t, 0.22, 0.3 * v, FilterType.Highpass, 1500, 1, 0.7); // 첨벙
            double[] mix = c.NewBuffer();
            double[] ks = { 1, 1.26, 1.5, 2 };
            for (int i = 0; i < ks.Length; i++)
            {
                double f0 = 1046 * ks[i] * p;
                c.Blip(mix, t + 0.1 * L + i * 0.07 * L, f0, f0 * 1.01, 0.18, 0.06 * v, Waveform.Triangle);
            }
            c.FeedbackDelay(mix, 0.12, 0.35, d);
        }

        // 불꽃(파이어볼): 발사 "화악"(로우패스가 열리는 노이즈) + 화염 몸통(일렁이는 브라운) + 타닥 + 착탄 "쿵·콰".
        static void Fire(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(6);
            double T = 1.0 * L, hit = 0.5 * L;
            c.Src(d, c.White, t, hit, 0.1, FilterType.Lowpass, 1.2,
                SynthBus.Env(t, 300, (0, 250), (0.1 * L, 3800 * p), (hit, 900 * p)),
                SynthBus.Env(t, 0, (0, 0), (0.06 * L, 0.45 * v), (0.38 * L, 0.2 * v), (hit, 0.05 * v)));
            var fl = new Automation(0).Set(0, t);
            for (double tt = 0; tt < T; tt += 0.03)
                fl.LinearTo((0.25 + 0.45 * r.Next()) * v * (tt < 0.05 ? tt / 0.05 : 1) * (1 - 0.7 * tt / T), t + tt);
            fl.LinearTo(0, t + T);
            c.Src(d, c.Brown, t, T, 0.5, FilterType.Lowpass, 0.8, Automation.Constant(800 * p), fl);
            for (int i = 0; i < 45; i++)
            {
                double gt = t + 0.03 + r.Next() * (T - 0.05);
                double dur = 0.002 + r.Next() * 0.005;
                double pk = (0.1 + 0.3 * r.Next()) * v;
                double f = 1500 + r.Next() * 4000;
                double off = r.Next() * 2;
                c.Grain(d, gt, dur, pk, FilterType.Bandpass, f, 2, off);
            }
            c.Blip(d, t + hit, 150 * p, 32, 0.4 * L, 0.6 * v); // 착탄 "쿵"
            c.Grain(d, t + hit, 0.45 * L, 0.9 * v, FilterType.Lowpass, 1200 * p, 1, 1.1, c.Brown); // 폭발 "콰"
            c.Grain(d, t + hit, 0.25 * L, 0.25 * v, FilterType.Bandpass, 2500, 0.7, 0.9);
        }

        // 풀: 바람 + 잎사귀 "사각사각" + 덩굴 채찍 "휙-착"(스윕 후 공명 스냅) + 나무 울림 "똑".
        static void Grass(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            var r = new Lcg(3);
            double T = 0.95 * L, snap = 0.58 * L;
            c.Src(d, c.White, t, T, 0.4, FilterType.Bandpass, 1.2,
                SynthBus.Env(t, 600 * p, (0, 450 * p), (0.35 * L, 2000 * p), (0.85 * L, 650 * p)),
                SynthBus.Env(t, 0, (0, 0), (0.3 * L, 0.3 * v), (0.85 * L, 0)));
            for (int i = 0; i < 80; i++)
            {
                double gt = t + r.Next() * 0.8 * L;
                double dur = 0.006 + r.Next() * 0.018;
                double pk = (0.05 + 0.15 * r.Next()) * v;
                double f = 3000 + r.Next() * 5000;
                double off = r.Next() * 2;
                c.Grain(d, gt, dur, pk, FilterType.Bandpass, f, 1.5, off);
            }
            double ws = t + snap - 0.13 * L;
            c.Src(d, c.White, ws, 0.13 * L, 1.3, FilterType.Bandpass, 2,
                SynthBus.Env(ws, 600, (0, 500), (0.13 * L, 6000)),
                SynthBus.Env(ws, 0, (0, 0), (0.1 * L, 0.35 * v), (0.13 * L, 0)));
            c.Grain(d, t + snap, 0.012, 0.6 * v, FilterType.Highpass, 2000, 1, 0.2);
            c.Grain(d, t + snap, 0.14, 2.2 * v, FilterType.Bandpass, 1100 * p, 14, 0.6);
            c.Blip(d, t + snap, 330 * p, 210 * p, 0.12, 0.25 * v, Waveform.Triangle);
        }

        // 비행: 날갯짓 "펄럭" 3번(점점 약해짐) + 돌풍 "휘익". 음정 있는 휘파람은 쓰지 않는다.
        static void Flying(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            double T = 0.8 * L;
            for (int i = 0; i < 3; i++)
            {
                double tt = t + i * 0.16 * L, a = (1.0 - 0.25 * i) * v;
                c.Src(d, c.White, tt, 0.13 * L, 0.2 + i * 0.37, FilterType.Lowpass, 0.8,
                    SynthBus.Env(tt, 700 * p, (0, 300 * p), (0.03 * L, 1100 * p), (0.12 * L, 350 * p)),
                    SynthBus.Env(tt, 0, (0, 0), (0.025 * L, 0.55 * a), (0.12 * L, 0)));
            }
            c.Src(d, c.White, t, T, 1.4, FilterType.Bandpass, 0.9,
                SynthBus.Env(t, 500, (0, 400 * p), (0.45 * L, 1700 * p), (T, 600 * p)),
                SynthBus.Env(t, 0, (0, 0), (0.4 * L, 0.3 * v), (T, 0)));
        }

        // 독: 13Hz 로 진폭이 떨리는(트레몰로) 탁한 사각파.
        static void Poison(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            double[] tmp = c.NewBuffer();
            c.Voice(tmp, t, Waveform.Square,
                new (double, double)[] { (0, 260 * p), (0.3 * L, 240 * p), (0.7 * L, 200 * p) },
                new (double, double)[] { (0, 0), (0.06, 0.12 * v), (0.7 * L, 0) },
                detuneCents: -30);
            var trem = new Lfo(13, 0.5, t, t + 0.8 * L); // trem.gain = 0.5 + 0.5·sin
            for (int n = 0; n < tmp.Length; n++)
                if (tmp[n] != 0) d[n] += tmp[n] * (0.5 + trem.ValueAt(c.TimeOf(n)));
        }

        // 고스트: 서로 ±14센트 어긋난 두 사인(맥놀이) + 오르내리는 음 + 6Hz 떨림, 아래에 삼각파.
        static void Ghost(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            foreach (double det in new[] { -14.0, 14.0 })
                c.Voice(d, t, Waveform.Sine,
                    new (double, double)[] { (0, 520 * p), (0.3 * L, 640 * p), (0.9 * L, 230 * p) },
                    new (double, double)[] { (0, 0), (0.25 * L, 0.22 * v), (0.6 * L, 0.18 * v), (0.9 * L, 0) },
                    6, 18 * p, det);
            c.Voice(d, t, Waveform.Triangle,
                new (double, double)[] { (0, 130 * p), (0.9 * L, 110 * p) },
                new (double, double)[] { (0, 0), (0.3 * L, 0.12 * v), (0.9 * L, 0) });
        }

        // 에스퍼: FM(변조기 사인 → 반송파 주파수)으로 일렁이는 금속성 톤 + 메아리(0.13초, 피드백 0.35).
        static void Psychic(SynthBus c, double[] d, double t, double p, double L, double v)
        {
            double[] mix = c.NewBuffer();
            var carF = SynthBus.Env(t, 440, (0, 480 * p), (0.3 * L, 720 * p), (0.95 * L, 420 * p));
            var modF = SynthBus.Env(t, 440, (0, 240 * p), (0.95 * L, 360 * p));
            var modDepth = SynthBus.Env(t, 1, (0, 0), (0.3 * L, 400 * p), (0.95 * L, 60 * p));
            var g = SynthBus.Env(t, 0, (0, 0), (0.1 * L, 0.2 * v), (0.7 * L, 0.16 * v), (0.95 * L, 0));
            var car = new Oscillator(Waveform.Sine, c.SampleRate);
            var mod = new Oscillator(Waveform.Sine, c.SampleRate);
            int a0 = c.IndexAtOrAfter(t), a1 = c.IndexAtOrAfter(t + 1.0 * L);
            for (int n = a0; n < a1; n++)
            {
                double tn = c.TimeOf(n);
                double m = mod.Next(modF.ValueAt(tn)) * modDepth.ValueAt(tn);
                mix[n] += car.Next(carF.ValueAt(tn) + m) * g.ValueAt(tn);
            }
            c.FeedbackDelay(mix, 0.13, 0.35, d);
        }
    }
}
