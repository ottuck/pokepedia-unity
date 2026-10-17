using System;

namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>배틀 화면(M3) 효과음 종류.</summary>
    public enum SfxKind
    {
        /// <summary>UI 커서 이동 "틱"</summary>
        CursorMove,
        /// <summary>결정 "띠링" (2음 상승)</summary>
        Confirm,
        /// <summary>취소 "띠둥" (2음 하강)</summary>
        Cancel,
        /// <summary>피격(일반)</summary>
        Hit,
        /// <summary>급소 피격(강한 타격)</summary>
        CriticalHit,
        /// <summary>빗나감 "휙"</summary>
        Miss,
        /// <summary>HP 감소 틱(게이지가 줄 때 반복 재생)</summary>
        HpTick,
        /// <summary>기절(하강음)</summary>
        Faint,
        /// <summary>승리 징글(짧은 상승 아르페지오)</summary>
        Victory,
        /// <summary>패배 징글</summary>
        Defeat,
        /// <summary>등장 "휙-뿅": 미끄러져 들어오는 바람 소리 후 번쩍임에 맞춘 짧은 상승음 (울음소리 아님)</summary>
        SendOut,
        /// <summary>기 모으기 "위이잉": 올라가는 펄스 + 떨림. 공격음이 시작될 때 끊는다</summary>
        Charge,
    }

    /// <summary>
    /// 8비트 느낌의 배틀 효과음 합성기. 펄스(듀티 12.5/25/50%)·삼각파·NES 식 LFSR 노이즈만 쓴다.
    /// 결과는 mono float[] [-1, 1], 종류별 목표 피크로 정규화한 뒤 리미터를 거친다. 결정적이다(난수 없음, LFSR 시드 고정).
    /// UnityEngine 을 쓰지 않는다. Unity 에서는 <c>AudioClip.Create(name, s.Length, 1, sampleRate, false)</c> + <c>SetData</c>.
    /// </summary>
    public static class BattleSfx
    {
        public const int DefaultSampleRate = TypeAttackSounds.DefaultSampleRate;

        // 음이름 주파수(Hz, A4 = 440 평균율)
        const double G3 = 196.00, C4 = 261.63, E4 = 329.63, F4 = 349.23, Fs4 = 369.99, G4 = 392.00;
        const double C5 = 523.25, E5 = 659.26, G5 = 783.99, B5 = 987.77, C6 = 1046.50, E6 = 1318.51, G6 = 1567.98;

        public static float[] Render(SfxKind kind, int sampleRate = DefaultSampleRate)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            double seconds = Duration(kind);
            var c = new SynthBus(sampleRate, seconds);
            double[] d = c.NewBuffer();
            double peak;
            switch (kind)
            {
                case SfxKind.CursorMove: peak = CursorMove(c, d); break;
                case SfxKind.Confirm: peak = Confirm(c, d); break;
                case SfxKind.Cancel: peak = Cancel(c, d); break;
                case SfxKind.Hit: peak = Hit(c, d); break;
                case SfxKind.CriticalHit: peak = CriticalHit(c, d); break;
                case SfxKind.Miss: peak = Miss(c, d); break;
                case SfxKind.HpTick: peak = HpTick(c, d); break;
                case SfxKind.Faint: peak = Faint(c, d); break;
                case SfxKind.Victory: peak = Victory(c, d); break;
                case SfxKind.Defeat: peak = Defeat(c, d); break;
                case SfxKind.SendOut: peak = SendOut(c, d); break;
                case SfxKind.Charge: peak = Charge(c, d); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
            Dynamics.NormalizePeak(d, peak);
            Dynamics.Limit(d, sampleRate);
            return PadTo(Dynamics.TrimToFloat(d, sampleRate, 1e-4, 0.004), sampleRate, MinSeconds);
        }

        /// <summary>
        /// 구운 효과음의 최소 길이. 웹 빌드는 소리를 압축해 넣는데, 아이폰 사파리는 0.05초 남짓한 소리(커서·HP 틱)를
        /// 풀지 못해 "EncodingError: Decoding failed" 를 낸다(2026-10-17 실기기). 뒤에 무음을 붙여 이 길이를 채운다.
        /// </summary>
        public const double MinSeconds = 0.25;

        static float[] PadTo(float[] samples, int sampleRate, double seconds)
        {
            int n = (int)Math.Ceiling(seconds * sampleRate);
            if (samples.Length >= n) return samples;
            var padded = new float[n];
            Array.Copy(samples, padded, samples.Length);
            return padded;
        }

        /// <summary>종류별 렌더 길이(초). 실제 결과는 꼬리 무음을 잘라 이보다 짧을 수 있다.</summary>
        public static double Duration(SfxKind kind)
        {
            switch (kind)
            {
                case SfxKind.CursorMove: return 0.05;
                case SfxKind.Confirm: return 0.17;
                case SfxKind.Cancel: return 0.15;
                case SfxKind.Hit: return 0.24;
                case SfxKind.CriticalHit: return 0.5;
                case SfxKind.Miss: return 0.32;
                case SfxKind.HpTick: return 0.05;
                case SfxKind.Faint: return 1.05;
                case SfxKind.Victory: return 1.0;
                case SfxKind.Defeat: return 1.2;
                case SfxKind.SendOut: return SendOutPopAt + 0.16;
                case SfxKind.Charge: return 0.8;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        // ---------------------------------------------------------------- 도구

        /// <summary>
        /// 음 하나: 펄스/삼각파, 3ms 어택 → (decay=true 면 지수 감쇠, 아니면 유지 후 마지막 release 초 동안 선형 감소).
        /// </summary>
        static void Note(SynthBus c, double[] d, double start, double dur, double freq, double peak,
            Waveform wave = Waveform.Pulse, double duty = 0.5, bool decay = false, double release = 0.02, Lfo vibrato = null)
        {
            var gain = new Automation(0).Set(0, start).LinearTo(peak, start + 0.003);
            if (decay) gain.ExpTo(0.001, start + dur);
            else gain.LinearTo(peak, start + Math.Max(0.003, dur - release)).LinearTo(0, start + dur);
            c.Osc(d, wave, start, start + dur, Automation.Constant(freq), gain, freqLfo: vibrato, duty: duty);
        }

        /// <summary>LFSR 노이즈(클록 주파수 자동화) × 게인 자동화 → d.</summary>
        static void NesNoise(SynthBus c, double[] d, double start, double stop, Automation clockHz, Automation gain, bool shortMode = false)
        {
            var nz = new LfsrNoise(c.SampleRate, shortMode);
            int a = c.IndexAtOrAfter(start), b = c.IndexAtOrAfter(stop);
            for (int n = a; n < b; n++)
            {
                double t = c.TimeOf(n);
                d[n] += nz.Next(clockHz.ValueAt(t)) * gain.ValueAt(t);
            }
        }

        // ---------------------------------------------------------------- 레시피 (반환값 = 목표 피크)

        static double CursorMove(SynthBus c, double[] d)
        {
            Note(c, d, 0, 0.045, E6, 1, Waveform.Pulse, 0.5, decay: true);
            return 0.4;
        }

        static double Confirm(SynthBus c, double[] d)
        {
            Note(c, d, 0, 0.055, B5, 1, Waveform.Pulse, 0.25, release: 0.008);
            Note(c, d, 0.055, 0.11, E6, 1, Waveform.Pulse, 0.25, decay: true);
            return 0.5;
        }

        static double Cancel(SynthBus c, double[] d)
        {
            Note(c, d, 0, 0.05, G5, 1, Waveform.Pulse, 0.25, release: 0.008);
            Note(c, d, 0.05, 0.095, G4, 1, Waveform.Pulse, 0.25, decay: true);
            return 0.45;
        }

        // 피격: LFSR 노이즈 버스트(클록이 내려가며 탁해짐) + 아래로 떨어지는 사각파 "퍽".
        static double Hit(SynthBus c, double[] d)
        {
            NesNoise(c, d, 0, 0.22,
                new Automation(9000).Set(9000, 0).ExpTo(900, 0.2),
                new Automation(0).Set(0, 0).LinearTo(1, 0.002).ExpTo(0.001, 0.22));
            c.Osc(d, Waveform.Square, 0, 0.15,
                new Automation(300).Set(300, 0).ExpTo(55, 0.13),
                new Automation(0).Set(0, 0).LinearTo(0.8, 0.003).ExpTo(0.001, 0.15));
            return 0.8;
        }

        // 급소: 날카로운 크랙 + 더 깊은 저음 낙하 + 길게 끄는 노이즈 + 70ms 뒤 두 번째 타격.
        static double CriticalHit(SynthBus c, double[] d)
        {
            NesNoise(c, d, 0, 0.03, Automation.Constant(20000),
                new Automation(0).Set(0, 0).LinearTo(1, 0.001).LinearTo(0.6, 0.03));
            c.Osc(d, Waveform.Square, 0, 0.36,
                new Automation(420).Set(420, 0).ExpTo(35, 0.32),
                new Automation(0).Set(0, 0).LinearTo(1, 0.003).ExpTo(0.001, 0.36));
            NesNoise(c, d, 0.02, 0.48,
                new Automation(7000).Set(7000, 0.02).ExpTo(300, 0.46),
                new Automation(0).Set(0, 0.02).LinearTo(0.8, 0.025).ExpTo(0.001, 0.48));
            NesNoise(c, d, 0.07, 0.13, Automation.Constant(14000),
                new Automation(0).Set(0, 0.07).LinearTo(0.9, 0.072).ExpTo(0.001, 0.13), shortMode: true);
            c.Osc(d, Waveform.Triangle, 0.07, 0.3,
                new Automation(160).Set(160, 0.07).ExpTo(40, 0.28),
                new Automation(0).Set(0, 0.07).LinearTo(0.9, 0.073).ExpTo(0.001, 0.3));
            return 0.95;
        }

        // 빗나감: LFSR 노이즈를 밴드패스로 위로 쓸었다 내리는 "휙".
        static double Miss(SynthBus c, double[] d)
        {
            var nz = new LfsrNoise(c.SampleRate);
            var bp = new Biquad(FilterType.Bandpass, 2.5, c.SampleRate);
            var f = new Automation(500).Set(500, 0).ExpTo(3800, 0.17).ExpTo(1200, 0.3);
            var g = new Automation(0).Set(0, 0).LinearTo(1, 0.13).LinearTo(0, 0.3);
            int b = c.IndexAtOrAfter(0.3);
            for (int n = 0; n < b; n++)
            {
                double t = c.TimeOf(n);
                d[n] += bp.Process(nz.Next(16000), f.ValueAt(t)) * g.ValueAt(t);
            }
            return 0.6;
        }

        static double HpTick(SynthBus c, double[] d)
        {
            Note(c, d, 0, 0.035, 1760, 1, Waveform.Pulse, 0.125, decay: true);
            return 0.25;
        }

        // 기절: 50% 펄스가 900Hz → 70Hz 로 지수 하강, 9Hz 떨림, 한 옥타브 아래 삼각파가 받친다. 끝으로 갈수록 사라짐.
        static double Faint(SynthBus c, double[] d)
        {
            var g = new Automation(0).Set(0, 0).LinearTo(0.9, 0.01).LinearTo(0.8, 0.55).LinearTo(0, 1.0);
            c.Osc(d, Waveform.Pulse, 0, 1.0, new Automation(900).Set(900, 0).ExpTo(70, 0.95), g,
                freqLfo: new Lfo(9, 12, 0.1), duty: 0.5);
            var g2 = new Automation(0).Set(0, 0).LinearTo(0.6, 0.01).LinearTo(0.6, 0.55).LinearTo(0, 1.0);
            c.Osc(d, Waveform.Triangle, 0, 1.0, new Automation(450).Set(450, 0).ExpTo(35, 0.95), g2);
            return 0.5; // 계속 울리는 소리라 피크를 낮춰 체감 음량을 다른 효과음과 맞춘다
        }

        // 승리: 25% 펄스 C5-E5-G5 상승 후 C6 를 길게, 아래에 12.5% 펄스 E5 와 삼각파 C4 가 화음.
        static double Victory(SynthBus c, double[] d)
        {
            double[] arp = { C5, E5, G5 };
            for (int i = 0; i < arp.Length; i++) Note(c, d, i * 0.08, 0.075, arp[i], 0.8, Waveform.Pulse, 0.25, release: 0.01);
            double s = 0.24;
            Note(c, d, s, 0.7, C6, 0.8, Waveform.Pulse, 0.25, release: 0.3, vibrato: new Lfo(6, 6, s + 0.2));
            Note(c, d, s, 0.7, E5, 0.45, Waveform.Pulse, 0.125, release: 0.3);
            Note(c, d, s, 0.72, C4, 0.9, Waveform.Triangle, release: 0.3);
            return 0.7;
        }

        /// <summary>등장음에서 "뿅"이 나는 시각. 연출의 미끄러져 들어오기(0.35초)가 끝나고 번쩍이는 순간과 맞춘다.</summary>
        public const double SendOutPopAt = 0.35;

        // 등장: 밴드패스 LFSR 노이즈가 위로 쓸리며 커지는 "휙"(미끄러짐) → 번쩍임에 맞춰 12.5% 펄스 G5-C6-G6 "뿅".
        static double SendOut(SynthBus c, double[] d)
        {
            var nz = new LfsrNoise(c.SampleRate);
            var bp = new Biquad(FilterType.Bandpass, 2, c.SampleRate);
            var f = new Automation(400).Set(400, 0).ExpTo(2600, SendOutPopAt);
            var g = new Automation(0).Set(0, 0).LinearTo(0.5, SendOutPopAt * 0.8).LinearTo(0, SendOutPopAt + 0.02);
            int b = c.IndexAtOrAfter(SendOutPopAt + 0.02);
            for (int n = 0; n < b; n++)
            {
                double t = c.TimeOf(n);
                d[n] += bp.Process(nz.Next(12000), f.ValueAt(t)) * g.ValueAt(t);
            }
            double s = SendOutPopAt;
            Note(c, d, s, 0.03, G5, 0.9, Waveform.Pulse, 0.125, release: 0.005);
            Note(c, d, s + 0.03, 0.03, C6, 0.9, Waveform.Pulse, 0.125, release: 0.005);
            Note(c, d, s + 0.06, 0.09, G6, 0.9, Waveform.Pulse, 0.125, decay: true);
            return 0.5;
        }

        // 기 모으기: 25% 펄스가 180Hz → 900Hz 로 오르며 18Hz 로 떨리고 점점 커진다. 아래에 한 옥타브 낮은 삼각파.
        // 길게 만들어 두고 공격음이 시작될 때 재생을 끊는다(기 모으는 시간은 종마다 다르다, animation.json windupSec).
        static double Charge(SynthBus c, double[] d)
        {
            const double T = 0.8;
            var freq = new Automation(180).Set(180, 0).ExpTo(900, T);
            var g = new Automation(0).Set(0, 0).LinearTo(0.35, 0.05).LinearTo(0.9, T - 0.05).LinearTo(0, T);
            var vib = new Lfo(18, 25, 0.05);
            c.Osc(d, Waveform.Pulse, 0, T, freq, g, freqLfo: vib, duty: 0.25);
            c.Osc(d, Waveform.Triangle, 0, T, new Automation(90).Set(90, 0).ExpTo(450, T), g);
            return 0.4;
        }

        // 패배: 반음씩 내려가는 G4-F#4-F4 후 E4 를 떨며 길게(마지막은 아래로 처짐), 삼각파 C4→G3 저음.
        static double Defeat(SynthBus c, double[] d)
        {
            double[] down = { G4, Fs4, F4 };
            for (int i = 0; i < down.Length; i++) Note(c, d, i * 0.18, 0.16, down[i], 0.8, Waveform.Pulse, 0.5, release: 0.03);
            double s = 0.54;
            var g = new Automation(0).Set(0, s).LinearTo(0.8, s + 0.005).LinearTo(0.6, s + 0.3).LinearTo(0, s + 0.62);
            c.Osc(d, Waveform.Pulse, s, s + 0.62, new Automation(E4).Set(E4, s).Set(E4, s + 0.3).ExpTo(E4 * 0.94, s + 0.62), g,
                freqLfo: new Lfo(5.5, 5, s + 0.05), duty: 0.5);
            Note(c, d, 0, 0.36, C4, 0.7, Waveform.Triangle, release: 0.03);
            Note(c, d, 0.36, 0.8, G3, 0.8, Waveform.Triangle, release: 0.4);
            return 0.45;
        }
    }
}
