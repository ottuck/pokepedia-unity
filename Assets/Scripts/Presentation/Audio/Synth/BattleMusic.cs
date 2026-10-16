using System;
using System.Collections.Generic;

namespace Pokepedia.Presentation.Audio.Synth
{
    /// <summary>
    /// 야생 배틀 BGM(오리지널 곡) 한 루프를 8비트 4채널로 합성한다. E 단조, 168 BPM, 4/4, 28마디(정확히 40초).
    /// 채널: 펄스1 리드(25% 듀티, 긴 음에 늦게 들어오는 비브라토 + 약한 메아리) / 펄스2 화성(아르페지오·이중 리드·엇박 스탭)
    /// / 삼각파 베이스(옥타브 패턴) / LFSR 노이즈 드럼(킥·스네어·하이햇·크래시).
    /// 악보는 아래 표(마디마다 16칸 = 16분음표)로 적혀 있어 사람이 고칠 수 있다.
    /// 루프 경계: 루프 끝을 넘는 소리(메아리·드럼 꼬리)를 처음에 겹쳐 더해(접기) 이어 재생이 끊김 없이 이어진다.
    /// 결정적이다(난수 없음, LFSR 시드 고정). UnityEngine 을 쓰지 않는다.
    /// Unity 에서는 <c>AudioClip.Create(name, s.Length, 1, sampleRate, false)</c> + <c>SetData</c>, <c>AudioSource.loop = true</c>.
    /// </summary>
    public static class BattleMusic
    {
        public const double Bpm = 168;
        public const int StepsPerBar = 16;

        /// <summary>16분음표 한 칸의 길이(초).</summary>
        public static double StepSeconds => 60.0 / Bpm / 4.0;

        public static int BarCount => Chords.Length;

        /// <summary>한 루프의 정확한 길이(초). 28마디 × 4박 × 60/168 = 40초.</summary>
        public static double LoopSeconds => BarCount * StepsPerBar * StepSeconds;

        /// <summary>한 루프의 샘플 수 = round(LoopSeconds × sampleRate). <see cref="Render"/> 결과 길이와 같다.</summary>
        public static int LoopSampleCount(int sampleRate) => (int)Math.Round(LoopSeconds * sampleRate);

        // ================================================================ 악보
        //
        // 표기(한 칸 = 16분음표, 마디당 16칸, 공백으로 구분):
        //   음이름+옥타브(E5, F#5, Bb4) = 새 음,  "-" = 앞 음을 이어서 끈다(마디를 넘어 이어도 된다),  "." = 쉼.
        //   펄스2 의 숫자 0~3 = 그 마디 화음의 자리바꿈 음(아래에서부터), 4~7 = 그 한 옥타브 위.
        //   베이스: R = 근음(E2~D#3 사이), O = 근음 한 옥타브 위, 5 = 화음 근음의 5도.
        //   드럼: K 킥, S 스네어, s 약한 스네어, r 롤 스네어(마디 안에서 점점 세게), h 닫힌 하이햇, o 열린 하이햇, C 크래시+킥.
        //
        // 구성(28마디):
        //   A  (1–8)   메인 훅. 펄스2 는 16분 아르페지오.           Em Em C D | Em C Am B7
        //   A' (9–16)  훅 변주, 더 높이 올라간다. 펄스2 는 3도 아래 이중 리드.  Em Em C D | Em C Am D7
        //   B  (17–24) 나란한조 G 장조로 밝게, 길게 노래하는 선율. 베이스가 한 음씩 내려간다. 펄스2 는 엇박 스탭.
        //                                                          G D/F# Em Bm | C G/B Am D
        //   T  (25–28) 되돌이 빌드업: 당김음 스탭 → 상승 아르페지오 → 스네어 롤, B(딸림화음)에서 1마디 Em 으로 해결.
        //                                                          C Am B B

        static readonly string[] Chords =
        {
            // A
            "Em", "Em", "C", "D", "Em", "C", "Am", "B7",
            // A'
            "Em", "Em", "C", "D", "Em", "C", "Am", "D7",
            // B
            "G", "D/F#", "Em", "Bm", "C", "G/B", "Am", "D",
            // T
            "C", "Am", "B", "B",
        };

        // 펄스1 리드 (25% 듀티)
        static readonly string[] Lead =
        {
            // ---- A: 메인 훅 (질문 2마디 + 대답 2마디, 두 번)
            "E5 -  E5 F#5 G5 -  -  B5 -  -  A5 -  G5 -  F#5 -",   //  1 Em  훅: 같은 음 두 번 → 계단 상승 → 5도 도약 후 내려옴
            "E5 -  -  -   -  -  .  .  B4 -  D5 -  E5 -  G5  -",   //  2 Em  길게 → 다음 마디로 끌어올리는 못갖춘 음
            "A5 -  -  G5  -  -  E5 -  C6 -  -  -  B5 -  A5  -",   //  3 C
            "B5 -  -  -   -  -  A5 -  F#5 - D5 -  F#5 - A5  -",   //  4 D
            "E5 -  E5 F#5 G5 -  -  B5 -  -  D6 -  B5 -  G5  -",   //  5 Em  훅 반복, 끝을 위로
            "E6 -  -  -   -  -  D6 -  C6 -  B5 -  A5 -  G5  -",   //  6 C   꼭대기에서 음계로 하강
            "A5 -  -  C6  -  -  E6 -  D6 -  C6 -  B5 -  A5  -",   //  7 Am
            "B5 -  -  -   -  -  A5 -  F#5 - D#5 - B4 -  D#5 -",   //  8 B7  이끈음 D# → 다음 마디 E
            // ---- A': 훅 변주
            "E5 -  E5 F#5 G5 -  -  B5 -  -  A5 -  G5 -  F#5 -",   //  9 Em  훅 그대로
            "E5 -  -  -   -  -  .  .  G5 -  A5 -  B5 -  D6  -",   // 10 Em  더 높이 끌어올림
            "E6 -  -  D6  -  -  C6 -  G5 -  -  -  A5 -  B5  -",   // 11 C
            "A5 -  -  -   -  -  F#5 - D5 -  F#5 - A5 -  D6  -",   // 12 D
            "B5 -  E6 -   -  -  B5 -  G5 -  E6 -  D6 -  B5  -",   // 13 Em  당김음 도약
            "C6 -  -  -   G5 -  E5 -  C6 -  D6 -  E6 -  -   -",   // 14 C
            "E6 -  -  D6  -  -  C6 -  B5 -  A5 -  C6 -  E6  -",   // 15 Am
            "D6 -  -  -   -  -  C6 -  A5 -  F#5 - A5 -  C6  -",   // 16 D7  G 장조로 넘어가는 딸림7
            // ---- B: G 장조, 길게 노래
            "D6 -  -  -   -  -  B5 -  G5 -  -  -  A5 -  B5  -",   // 17 G
            "A5 -  -  -   -  -  F#5 - D5 -  -  -  E5 -  F#5 -",   // 18 D/F#  17마디를 3도 아래로 반복
            "G5 -  -  -   A5 -  B5 -  E6 -  -  -  -  -  D6  -",   // 19 Em    D6 는 다음 마디로 이어 끈다(당김)
            "-  -  -  -   -  -  -  -  B5 -  A5 -  F#5 - A5  -",   // 20 Bm
            "E6 -  -  -   -  -  D6 -  C6 -  -  -  B5 -  C6  -",   // 21 C     17마디 리듬 되풀이
            "D6 -  -  -   -  -  B5 -  G5 -  A5 -  B5 -  D6  -",   // 22 G/B
            "C6 -  -  -   -  -  B5 -  A5 -  E5 -  A5 -  C6  -",   // 23 Am
            "A5 -  -  -   -  -  -  -  F#5 - A5 -  D6 -  C6  -",   // 24 D
            // ---- T: 빌드업 → 1마디로
            "C6 -  -  C6  -  -  B5 -  C6 -  -  D6 -  -  E6  -",   // 25 C   3-3-2 당김음 스탭
            "E6 -  -  E6  -  -  D6 -  C6 -  -  B5 -  -  A5  -",   // 26 Am
            "B5 -  -  D#6 -  -  F#6 - B5 -  -  D#6 - -  F#6 -",   // 27 B   딸림화음 분산 상승
            "B5 -  -  -   -  -  -  -  A5 -  G5 -  F#5 - D#5 -",   // 28 B   스네어 롤 위에서 길게 → 내려와 이끈음 D# → 1마디 E5
        };

        // 펄스2 스타일. "이름: 패턴" (숫자 = 화음 음, 위 표기 참고). twin = 리드의 3도 아래(화음 음이 우선).
        const string Arp = "arp:  0 1 2 1 3 1 2 1 0 1 2 1 3 1 2 1";   // 12.5% 듀티 16분 아르페지오
        const string Stab = "stab: . . 1 - . . 2 - . . 1 - . . 3 -";  // 50% 듀티 엇박 8분 스탭
        const string Rise = "rise: 0 1 2 3 1 2 3 4 2 3 4 5 3 4 5 6";  // 25% 듀티 한 옥타브 넘게 오르는 아르페지오
        const string Twin = "twin";                                    // 50% 듀티 이중 리드

        static readonly string[] Pulse2 =
        {
            Arp, Arp, Arp, Arp, Arp, Arp, Arp, Arp,             // A
            Twin, Twin, Twin, Twin, Twin, Twin, Twin, Twin,     // A'
            Stab, Stab, Stab, Stab, Stab, Stab, Stab, Stab,     // B
            Rise, Rise, Rise, Rise,                             // T
        };

        // 삼각파 베이스
        const string Gallop = "R . O . R R O . R . O . R R O .";  // 2·4박에 말발굽 리듬
        const string Steady = "R - O - R - O - R - O - 5 - O -";  // 길게 이어지는 옥타브(B 의 하행 베이스)
        const string Push = "R R O . R R O . R R O . R R O .";    // 빌드업
        const string Drive = "R O R O R O R O R O R O R O R O";   // 16분 옥타브 연타

        static readonly string[] Bass =
        {
            Gallop, Gallop, Gallop, Gallop, Gallop, Gallop, Gallop, Gallop,   // A
            Gallop, Gallop, Gallop, Gallop, Gallop, Gallop, Gallop, Gallop,   // A'
            Steady, Steady, Steady, Steady, Steady, Steady, Steady, Steady,   // B
            Push, Push, Drive, Drive,                                         // T
        };

        // LFSR 노이즈 드럼
        const string Rock = "K . h . S . h K . K h . S . h o";
        const string RockCrash = "C . h . S . h K . K h . S . h o";
        const string FillA = "K . h . S . h K . K h . S s S S";
        const string FillA2 = "K . h . S . h K S . S s S S S S";
        const string Lift = "K . o . S . o . K K o . S . o .";
        const string LiftCrash = "C . o . S . o . K K o . S . o .";
        const string FillB = "K . o . S . o . K K o . S s S S";
        const string Build = "K . h K S . h K K . h K S . h K";
        const string BuildCrash = "C . h K S . h K K . h K S . h K";
        const string Build2 = "K . S . K . S . K . S . K S S S";
        const string Roll = "K r r r r r r r r r r r r r r r";

        static readonly string[] Drums =
        {
            RockCrash, Rock, Rock, Rock, Rock, Rock, Rock, FillA,    // A
            RockCrash, Rock, Rock, Rock, Rock, Rock, Rock, FillA2,   // A'
            LiftCrash, Lift, Lift, Lift, Lift, Lift, Lift, FillB,    // B
            BuildCrash, Build, Build2, Roll,                         // T
        };

        // ================================================================ 믹스·음색 상수

        /// <summary>믹스 후 목표: 피크 0.5 이하, RMS −21 dBFS(둘 중 먼저 닿는 쪽에 맞춘다). 효과음 아래에 깔린다.</summary>
        const double PeakTarget = 0.5;
        const double RmsTargetDb = -21;

        /// <summary>루프 끝을 넘어 울리는 소리(메아리·드럼 꼬리)를 담는 여유. 접어서 처음에 더한다.</summary>
        const double TailSeconds = 3.0;

        // 채널 게인(스템 순서: 리드, 펄스2, 삼각파, 노이즈)
        const double LeadGain = 0.30, Pulse2Gain = 0.20, TriangleGain = 0.42, NoiseGain = 0.45;

        internal static readonly string[] StemNames = { "pulse1 lead", "pulse2", "triangle", "noise" };

        // 리드 비브라토: 0.33초 이상 음에만, 0.16초 뒤 0.15초에 걸쳐 들어온다. 5.6Hz, ±0.6%(약 ±10센트).
        const double VibMinSeconds = 0.33, VibDelay = 0.16, VibFade = 0.15, VibRate = 5.6, VibDepth = 0.006;

        // 리드 메아리: 점8분(3칸) 지연, 피드백 0.3, 젖은 소리 0.22.
        const int EchoSteps = 3;
        const double EchoFeedback = 0.3, EchoWet = 0.22;

        sealed class Env
        {
            public readonly double Attack, DecayTo, DecayTau, Release;
            public Env(double attack, double decayTo, double decayTau, double release)
            {
                Attack = attack; DecayTo = decayTo; DecayTau = decayTau; Release = release;
            }
        }

        // 어택(선형) → DecayTo 까지 지수 감쇠(NES 볼륨 엔벨로프 느낌) → 음 끝 Release 동안 선형으로 0. 클릭 방지용 램프가 항상 있다.
        static readonly Env LeadEnv = new Env(0.003, 0.72, 0.09, 0.022);
        static readonly Env ArpEnv = new Env(0.002, 0.55, 0.06, 0.012);
        static readonly Env StabEnv = new Env(0.002, 0.30, 0.05, 0.015);
        static readonly Env TwinEnv = new Env(0.003, 0.70, 0.08, 0.02);
        static readonly Env BassEnv = new Env(0.002, 1.0, 1.0, 0.008); // 삼각파는 NES 처럼 음량 일정, 켜고 끄는 램프만

        sealed class Style
        {
            public readonly double Duty, Level;
            public readonly Env Env;
            public Style(double duty, double level, Env env) { Duty = duty; Level = level; Env = env; }
        }

        static Style StyleOf(string name)
        {
            switch (name)
            {
                case "arp": return new Style(0.125, 0.85, ArpEnv);
                case "stab": return new Style(0.5, 0.75, StabEnv);
                case "rise": return new Style(0.25, 0.75, ArpEnv);
                case "twin": return new Style(0.5, 0.70, TwinEnv);
                default: throw new InvalidOperationException($"BattleMusic: 모르는 펄스2 스타일 '{name}'");
            }
        }

        // ================================================================ 렌더

        /// <summary>한 루프(mono, [-1, 1], 길이 = <see cref="LoopSampleCount"/>). 이어 붙여 반복해도 경계가 매끄럽다.</summary>
        public static float[] Render(int sampleRate = TypeAttackSounds.DefaultSampleRate)
        {
            if (sampleRate < 8000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            double[][] stems = RenderStems(sampleRate);
            int n = stems[0].Length;
            var mix = new double[n];
            foreach (double[] s in stems)
                for (int i = 0; i < n; i++) mix[i] += s[i];

            // 직류 제거(루프 전체 평균 = 상수라 경계 연속성에 영향 없음)
            double mean = 0;
            for (int i = 0; i < n; i++) mean += mix[i];
            mean /= n;
            double peak = 0, sum = 0;
            for (int i = 0; i < n; i++)
            {
                mix[i] -= mean;
                double a = Math.Abs(mix[i]);
                if (a > peak) peak = a;
                sum += mix[i] * mix[i];
            }
            double rms = Math.Sqrt(sum / n);
            double k = peak > 1e-9 ? Math.Min(PeakTarget / peak, Math.Pow(10, RmsTargetDb / 20) / rms) : 1;

            var y = new float[n];
            for (int i = 0; i < n; i++) y[i] = (float)(mix[i] * k);
            return y;
        }

        /// <summary>채널별 결과(믹스 게인 적용, 루프 길이로 접은 뒤, 정규화 전). 렌더 도구가 채널 균형을 볼 때도 쓴다.</summary>
        internal static double[][] RenderStems(int sampleRate)
        {
            Validate();
            int loop = LoopSampleCount(sampleRate);
            int total = loop + (int)Math.Ceiling(TailSeconds * sampleRate);
            double step = StepSeconds;
            var chords = new Chord[BarCount];
            for (int b = 0; b < BarCount; b++) chords[b] = ParseChord(Chords[b]);

            // ---- 펄스1 리드
            List<Ev> leadNotes = SequenceLead();
            var lead = new double[total];
            foreach (Ev e in leadNotes)
                Tone(lead, sampleRate, Waveform.Pulse, 0.25, e.Start * step, e.Len * step, MidiHz(e.Midi), 1.0, LeadEnv, vibrato: true);
            Echo(lead, (int)Math.Round(EchoSteps * step * sampleRate), EchoFeedback, EchoWet);

            // ---- 펄스2
            var p2 = new double[total];
            for (int b = 0; b < BarCount; b++)
            {
                string spec = Pulse2[b];
                int colon = spec.IndexOf(':');
                string styleName = (colon >= 0 ? spec.Substring(0, colon) : spec).Trim();
                Style st = StyleOf(styleName);
                List<Ev> notes;
                if (styleName == "twin")
                {
                    notes = new List<Ev>();
                    foreach (Ev e in leadNotes)
                        if (e.Start / StepsPerBar == b) notes.Add(new Ev(e.Start, e.Len, HarmonyBelow(e.Midi, chords[b])));
                }
                else
                {
                    Chord ch = chords[b];
                    notes = SequenceBar(spec.Substring(colon + 1), b, tok =>
                    {
                        int idx = tok[0] - '0';
                        if (tok.Length != 1 || idx < 0 || idx > 7) throw new InvalidOperationException($"BattleMusic: 펄스2 {b + 1}마디 '{tok}'");
                        return ch.Voicing[idx % 4] + 12 * (idx / 4);
                    });
                }
                foreach (Ev e in notes)
                    Tone(p2, sampleRate, Waveform.Pulse, st.Duty, e.Start * step, e.Len * step, MidiHz(e.Midi), st.Level, st.Env, vibrato: false);
            }

            // ---- 삼각파 베이스
            var tri = new double[total];
            for (int b = 0; b < BarCount; b++)
            {
                Chord ch = chords[b];
                List<Ev> notes = SequenceBar(Bass[b], b, tok =>
                {
                    switch (tok)
                    {
                        case "R": return ch.BassLow;
                        case "O": return ch.BassLow + 12;
                        case "5": return ch.BassLow + Mod(ch.Root + 7 - ch.BassLow, 12);
                        default: throw new InvalidOperationException($"BattleMusic: 베이스 {b + 1}마디 '{tok}'");
                    }
                });
                foreach (Ev e in notes)
                    Tone(tri, sampleRate, Waveform.Triangle, 0.5, e.Start * step, e.Len * step, MidiHz(e.Midi), 1.0, BassEnv, vibrato: false);
            }

            // ---- 노이즈 드럼 (킥·스네어 몸통은 짧은 삼각파 스윕을 섞는다)
            var nz = new double[total];
            var lfsr = new LfsrNoise(sampleRate);
            for (int b = 0; b < BarCount; b++)
            {
                string[] toks = Tokens(Drums[b]);
                for (int s = 0; s < StepsPerBar; s++)
                {
                    double t = (b * StepsPerBar + s) * step;
                    switch (toks[s])
                    {
                        case "K": Kick(nz, sampleRate, lfsr, t); break;
                        case "C":
                            Kick(nz, sampleRate, lfsr, t);
                            NoiseHit(nz, sampleRate, lfsr, t, 0.9, 16000, 7000, 0.42);
                            break;
                        case "S": Snare(nz, sampleRate, lfsr, t, 1.0, 0.16); break;
                        case "s": Snare(nz, sampleRate, lfsr, t, 0.4, 0.09); break;
                        case "r": Snare(nz, sampleRate, lfsr, t, 0.3 + 0.7 * s / (StepsPerBar - 1), 0.09); break;
                        case "h": NoiseHit(nz, sampleRate, lfsr, t, 0.035, 26000, 22000, 0.32); break;
                        case "o": NoiseHit(nz, sampleRate, lfsr, t, 0.15, 22000, 16000, 0.24); break;
                        case ".": break;
                        default: throw new InvalidOperationException($"BattleMusic: 드럼 {b + 1}마디 '{toks[s]}'");
                    }
                }
            }
            // 하이햇이 거칠지 않게 노이즈 버스 전체를 부드럽게 깎는다(8kHz 저역통과, Q −3dB ≈ 버터워스).
            var lp = new Biquad(FilterType.Lowpass, -3, sampleRate);
            for (int i = 0; i < total; i++) nz[i] = lp.Process(nz[i], 8000);

            // 모두 선형(소스 합 + LTI 처리)이므로, 루프 길이로 접으면 무한 반복 재생과 같은 결과가 된다.
            return new[]
            {
                Fold(lead, loop, LeadGain),
                Fold(p2, loop, Pulse2Gain),
                Fold(tri, loop, TriangleGain),
                Fold(nz, loop, NoiseGain),
            };
        }

        // ================================================================ 소리 도구

        /// <summary>음 하나(펄스/삼각파). 엔벨로프는 <see cref="Env"/>, 리드는 긴 음에 늦은 비브라토.</summary>
        static void Tone(double[] d, int sr, Waveform wave, double duty, double t0, double dur, double freq, double amp, Env env, bool vibrato)
        {
            int a = IndexAtOrAfter(t0, sr, d.Length), b = IndexAtOrAfter(t0 + dur, sr, d.Length);
            var osc = new Oscillator(wave, sr, duty);
            bool vib = vibrato && dur >= VibMinSeconds;
            for (int n = a; n < b; n++)
            {
                double t = (double)n / sr - t0;
                double g = env.DecayTo >= 1 ? 1 : env.DecayTo + (1 - env.DecayTo) * Math.Exp(-t / env.DecayTau);
                if (t < env.Attack) g *= t / env.Attack;
                double r = (dur - t) / env.Release;
                if (r < 1) g *= r > 0 ? r : 0;
                double f = freq;
                if (vib && t > VibDelay)
                {
                    double ramp = Math.Min(1, (t - VibDelay) / VibFade);
                    f *= 1 + VibDepth * ramp * Math.Sin(2 * Math.PI * VibRate * (t - VibDelay));
                }
                d[n] += osc.Next(f) * g * amp;
            }
        }

        /// <summary>LFSR 노이즈 한 방: 1ms 어택, dur 에 0.001 까지 지수 감쇠, 클록은 clock0 → clock1 지수 이동.</summary>
        static void NoiseHit(double[] d, int sr, LfsrNoise nz, double t0, double dur, double clock0, double clock1, double amp)
        {
            int a = IndexAtOrAfter(t0, sr, d.Length), b = IndexAtOrAfter(t0 + dur, sr, d.Length);
            double lnDecay = Math.Log(0.001), lnClock = Math.Log(clock1 / clock0);
            for (int n = a; n < b; n++)
            {
                double t = (double)n / sr - t0;
                double u = t / dur;
                double g = Math.Exp(lnDecay * u);
                if (t < 0.001) g *= t / 0.001;
                d[n] += nz.Next(clock0 * Math.Exp(lnClock * u)) * g * amp;
            }
        }

        /// <summary>짧은 음높이 하강(삼각파): 킥·스네어 몸통.</summary>
        static void Sweep(double[] d, int sr, double t0, double dur, double f0, double f1, double amp)
        {
            int a = IndexAtOrAfter(t0, sr, d.Length), b = IndexAtOrAfter(t0 + dur, sr, d.Length);
            var osc = new Oscillator(Waveform.Triangle, sr);
            double lnDecay = Math.Log(0.001), lnF = Math.Log(f1 / f0);
            for (int n = a; n < b; n++)
            {
                double t = (double)n / sr - t0;
                double u = t / dur;
                double g = Math.Exp(lnDecay * u);
                if (t < 0.001) g *= t / 0.001;
                d[n] += osc.Next(f0 * Math.Exp(lnF * u)) * g * amp;
            }
        }

        static void Kick(double[] d, int sr, LfsrNoise nz, double t)
        {
            NoiseHit(d, sr, nz, t, 0.05, 2400, 300, 0.55);
            Sweep(d, sr, t, 0.09, 170, 48, 0.9);
        }

        static void Snare(double[] d, int sr, LfsrNoise nz, double t, double vel, double dur)
        {
            NoiseHit(d, sr, nz, t, dur, 9000, 5000, 0.85 * vel);
            Sweep(d, sr, t, 0.05, 240, 150, 0.35 * vel);
        }

        /// <summary>정수 샘플 피드백 딜레이(메아리). x += wet·e, e[n] = line[n−D], line[n] = x[n] + fb·e[n].</summary>
        static void Echo(double[] x, int delay, double fb, double wet)
        {
            var line = new double[x.Length];
            for (int n = 0; n < x.Length; n++)
            {
                double e = n >= delay ? line[n - delay] : 0;
                double xn = x[n];
                line[n] = xn + fb * e;
                x[n] = xn + wet * e;
            }
        }

        /// <summary>y[i mod loop] += x[i]·gain. 루프 끝을 넘는 꼬리를 처음에 겹친다.</summary>
        static double[] Fold(double[] x, int loop, double gain)
        {
            var y = new double[loop];
            for (int i = 0; i < x.Length; i++) y[i % loop] += x[i] * gain;
            return y;
        }

        static int IndexAtOrAfter(double t, int sr, int length)
        {
            double x = Math.Ceiling(t * sr - 1e-9);
            if (x < 0) return 0;
            return x > length ? length : (int)x;
        }

        static double MidiHz(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);

        static int Mod(int a, int m) => ((a % m) + m) % m;

        // ================================================================ 악보 해석

        readonly struct Ev
        {
            public readonly int Start, Len, Midi; // Start·Len 은 16분음표 칸(루프 처음부터)
            public Ev(int start, int len, int midi) { Start = start; Len = len; Midi = midi; }
        }

        sealed class Chord
        {
            public int Root;      // 음높이 부류 0~11 (C = 0)
            public int BassLow;   // 베이스 R 음(E2~D#3, MIDI 40~51)
            public int[] Voicing; // 펄스2 용 4음(E4 이상에서 가장 낮은 자리, 오름차순)
        }

        static void Validate()
        {
            int bars = Chords.Length;
            if (Lead.Length != bars || Pulse2.Length != bars || Bass.Length != bars || Drums.Length != bars)
                throw new InvalidOperationException("BattleMusic: 채널별 마디 수가 다르다");
            for (int b = 0; b < bars; b++)
            {
                Tokens(Lead[b]); Tokens(Bass[b]); Tokens(Drums[b]);
                int colon = Pulse2[b].IndexOf(':');
                if (colon >= 0) Tokens(Pulse2[b].Substring(colon + 1));
            }
        }

        static string[] Tokens(string line)
        {
            string[] t = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length != StepsPerBar) throw new InvalidOperationException($"BattleMusic: 마디 칸 수 {t.Length} ≠ {StepsPerBar}: \"{line}\"");
            return t;
        }

        /// <summary>리드 전체(마디를 넘는 "-" 이음 허용).</summary>
        static List<Ev> SequenceLead()
        {
            var list = new List<Ev>();
            int curStart = -1, curLen = 0, curMidi = 0;
            for (int b = 0; b < Lead.Length; b++)
            {
                string[] toks = Tokens(Lead[b]);
                for (int s = 0; s < StepsPerBar; s++)
                {
                    string tok = toks[s];
                    int g = b * StepsPerBar + s;
                    if (tok == "-") { if (curStart >= 0) curLen++; continue; }
                    if (curStart >= 0) list.Add(new Ev(curStart, curLen, curMidi));
                    curStart = -1;
                    if (tok == ".") continue;
                    curStart = g; curLen = 1; curMidi = NoteMidi(tok);
                }
            }
            if (curStart >= 0) list.Add(new Ev(curStart, curLen, curMidi));
            return list;
        }

        /// <summary>한 마디(이음은 마디 안에서만).</summary>
        static List<Ev> SequenceBar(string line, int bar, Func<string, int> resolve)
        {
            var list = new List<Ev>();
            string[] toks = Tokens(line);
            int curStart = -1, curLen = 0, curMidi = 0;
            for (int s = 0; s < StepsPerBar; s++)
            {
                string tok = toks[s];
                if (tok == "-") { if (curStart >= 0) curLen++; continue; }
                if (curStart >= 0) list.Add(new Ev(curStart, curLen, curMidi));
                curStart = -1;
                if (tok == ".") continue;
                curStart = bar * StepsPerBar + s; curLen = 1; curMidi = resolve(tok);
            }
            if (curStart >= 0) list.Add(new Ev(curStart, curLen, curMidi));
            return list;
        }

        /// <summary>"E5", "F#5", "Bb4" → MIDI 번호(C4 = 60).</summary>
        static int NoteMidi(string s)
        {
            int i = 0;
            int pc = PitchClass(s, ref i);
            if (i >= s.Length || !int.TryParse(s.Substring(i), out int oct))
                throw new InvalidOperationException($"BattleMusic: 음이름 '{s}'");
            return (oct + 1) * 12 + pc;
        }

        static int PitchClass(string s, ref int i)
        {
            int pc;
            switch (char.ToUpperInvariant(s[i]))
            {
                case 'C': pc = 0; break;
                case 'D': pc = 2; break;
                case 'E': pc = 4; break;
                case 'F': pc = 5; break;
                case 'G': pc = 7; break;
                case 'A': pc = 9; break;
                case 'B': pc = 11; break;
                default: throw new InvalidOperationException($"BattleMusic: 음이름 '{s}'");
            }
            i++;
            while (i < s.Length && (s[i] == '#' || s[i] == 'b'))
            {
                pc += s[i] == '#' ? 1 : -1;
                i++;
            }
            return Mod(pc, 12);
        }

        /// <summary>"Em", "B7", "D/F#" 같은 화음 이름 해석. m = 단3화음, 7 = 딸림7(단7도), /X = 베이스 음.</summary>
        static Chord ParseChord(string name)
        {
            int slash = name.IndexOf('/');
            string main = slash >= 0 ? name.Substring(0, slash) : name;
            int i = 0;
            int root = PitchClass(main, ref i);
            string q = main.Substring(i);
            bool minor = q.StartsWith("m", StringComparison.Ordinal);
            bool seventh = q.Contains("7");
            int bassPc = root;
            if (slash >= 0) { int j = 0; bassPc = PitchClass(name.Substring(slash + 1), ref j); }

            var pcs = new List<int> { root, Mod(root + (minor ? 3 : 4), 12), Mod(root + 7, 12) };
            if (seventh) pcs.Add(Mod(root + 10, 12));
            var v = new List<int>();
            foreach (int pc in pcs) v.Add(64 + Mod(pc - 4, 12)); // E4(64) 이상에서 가장 낮은 자리
            v.Sort();
            if (v.Count == 3) v.Add(v[0] + 12);
            return new Chord { Root = root, BassLow = 40 + Mod(bassPc - 4, 12), Voicing = v.ToArray() };
        }

        /// <summary>
        /// 이중 리드 음: E 단조 음계에서 3도 아래. 단, 리드가 화음 음인데 3도 아래가 화음 밖이면
        /// 리드보다 3반음 이상 낮은 가장 가까운 화음 음을 쓴다(부딪힘 방지).
        /// </summary>
        static int HarmonyBelow(int lead, Chord ch)
        {
            int[] scale = { 4, 6, 7, 9, 11, 0, 2 }; // E 자연단음계
            bool InScale(int m) => Array.IndexOf(scale, Mod(m, 12)) >= 0;
            bool InChord(int m)
            {
                foreach (int v in ch.Voicing) if (Mod(v - m, 12) == 0) return true;
                return false;
            }

            int h;
            if (InScale(lead))
            {
                h = lead - 1;
                int found = 0;
                while (true)
                {
                    if (InScale(h) && ++found == 2) break;
                    h--;
                }
            }
            else h = lead - 4;

            if (InChord(lead) && !InChord(h))
            {
                int c = lead - 3;
                while (!InChord(c)) c--;
                h = c;
            }
            return h;
        }
    }
}
