using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Pokepedia.Presentation.Audio.Synth;

namespace Pokepedia.Tools.AudioRender
{
    /// <summary>
    /// 속성 공격음(8종)과 배틀 효과음, 배틀 BGM 루프를 16bit PCM mono WAV 로 렌더하고,
    /// (속성 소리는 포켓몬 울음소리가 아니라 기술을 쓸 때 나는 공격음이다)
    /// 길이·피크·RMS·클리핑·무음·결정성(해시)을 표로 보여 준다. 문제가 있으면 종료 코드 1.
    /// 실행(저장소 루트에서): <c>dotnet run -c Release --project Tools/audio/render</c>
    /// 선택 인자: <c>-- --sr 44100 --out 경로 --len 0.8</c> (기본 22050Hz, Tools/audio/out, 속성 공격음 길이 1 — 게임은 0.8).
    /// </summary>
    static class Program
    {
        sealed class Job
        {
            public string Name;
            public Func<float[]> Render;
        }

        static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            int sr = TypeAttackSounds.DefaultSampleRate;
            double len = 1; // 속성 공격음 길이 배수. 게임은 0.8(SoundBank.MoveSoundLength)
            string root = FindRepoRoot();
            string outDir = Path.Combine(root, "Tools", "audio", "out");
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--sr" && i + 1 < args.Length) sr = int.Parse(args[++i]);
                else if (args[i] == "--out" && i + 1 < args.Length) outDir = Path.GetFullPath(args[++i]);
                else if (args[i] == "--len" && i + 1 < args.Length) len = double.Parse(args[++i]);
            }
            Directory.CreateDirectory(outDir);

            var jobs = BuildJobs(root, sr, len);
            Console.WriteLine($"sampleRate={sr}  out={outDir}");
            Console.WriteLine();
            Console.WriteLine($"| {"file",-28} | {"sec",6} | {"peak",6} | {"peak dBFS",9} | {"RMS dBFS",8} | {"|x|>1",5} | sha256(앞 12) |");
            Console.WriteLine($"|{new string('-', 30)}|{new string('-', 8)}|{new string('-', 8)}|{new string('-', 11)}|{new string('-', 10)}|{new string('-', 7)}|{new string('-', 15)}|");

            var hashes = new Dictionary<string, string>();
            bool ok = true;
            foreach (var job in jobs)
            {
                float[] s = job.Render();
                string hash = Hash(s);
                hashes[job.Name] = hash;
                double peak = 0, sum = 0;
                int clipped = 0;
                foreach (float x in s)
                {
                    double a = Math.Abs(x);
                    if (a > peak) peak = a;
                    if (a > 1) clipped++;
                    sum += x * (double)x;
                }
                double rms = s.Length > 0 ? Math.Sqrt(sum / s.Length) : 0;
                bool silent = peak < 0.01 || rms < 0.001;
                if (clipped > 0 || silent || float.IsNaN(s.Sum())) ok = false;
                WriteWav(Path.Combine(outDir, job.Name + ".wav"), s, sr);
                Console.WriteLine($"| {job.Name + ".wav",-28} | {s.Length / (double)sr,6:F3} | {peak,6:F3} | {Db(peak),9:F1} | {Db(rms),8:F1} | {clipped,5} | {hash.Substring(0, 12)}{(silent ? " 무음!" : "")} |");
            }

            // 결정성: 역순으로 다시 렌더해(호출 순서·정적 상태 영향 확인) 해시를 비교한다.
            int mismatch = 0;
            foreach (var job in Enumerable.Reverse(jobs))
                if (Hash(job.Render()) != hashes[job.Name]) { mismatch++; Console.WriteLine($"결정성 불일치: {job.Name}"); }
            Console.WriteLine();
            Console.WriteLine(mismatch == 0 ? $"결정성: {jobs.Count}개 모두 재렌더 해시 일치" : $"결정성: {mismatch}개 불일치");
            Console.WriteLine(ok ? "검사: 클리핑 없음, 무음 없음" : "검사: 클리핑 또는 무음 발견");
            bool loopOk = CheckBgmLoop(sr);
            return ok && loopOk && mismatch == 0 ? 0 : 1;
        }

        static List<Job> BuildJobs(string root, int sr, double len)
        {
            var jobs = new List<Job>();
            foreach (AttackElement e in Enum.GetValues(typeof(AttackElement)))
            {
                AttackElement el = e;
                jobs.Add(new Job { Name = "type_" + el.ToString().ToLowerInvariant(), Render = () => TypeAttackSounds.Render(el, null, 0, len, sr) });
            }
            foreach (string id in TypeAttackSounds.AlternateIds) // 보관한 공격음(두 번째 기술용)
            {
                string a = id;
                jobs.Add(new Job { Name = "alt_" + a, Render = () => TypeAttackSounds.RenderAlternate(a, 0, len, sr) });
            }
            foreach (SfxKind k in Enum.GetValues(typeof(SfxKind)))
            {
                SfxKind kind = k;
                jobs.Add(new Job { Name = "sfx_" + ToSnake(kind.ToString()), Render = () => BattleSfx.Render(kind, sr) });
            }
            // 배틀 BGM: 한 루프, 그리고 이음매를 귀로 확인하려고 두 루프를 이어 붙인 것.
            jobs.Add(new Job { Name = "bgm_battle", Render = () => BattleMusic.Render(sr) });
            jobs.Add(new Job { Name = "bgm_battle_x2", Render = () => Repeat(BattleMusic.Render(sr), 2) });
            return jobs;
        }

        static float[] Repeat(float[] s, int times)
        {
            var r = new float[s.Length * times];
            for (int i = 0; i < times; i++) Array.Copy(s, 0, r, i * s.Length, s.Length);
            return r;
        }

        /// <summary>
        /// BGM 루프 검사: 길이 = round(LoopSeconds·sr), 이음매(마지막 → 첫 샘플) 차이가 루프 안쪽 최대 샘플 간 차이 이하,
        /// 직류 |평균| &lt; 0.001. 채널 균형(믹스 기준 RMS dB)도 보여 준다.
        /// </summary>
        static bool CheckBgmLoop(int sr)
        {
            float[] s = BattleMusic.Render(sr);
            int expected = BattleMusic.LoopSampleCount(sr);
            double mean = 0, maxStep = 0;
            var steps = new double[s.Length - 1];
            for (int i = 0; i < s.Length; i++)
            {
                mean += s[i];
                if (i > 0) { steps[i - 1] = Math.Abs(s[i] - s[i - 1]); if (steps[i - 1] > maxStep) maxStep = steps[i - 1]; }
            }
            mean /= s.Length;
            Array.Sort(steps);
            double p99 = steps[(int)(steps.Length * 0.99)];
            double seam = Math.Abs(s[0] - s[s.Length - 1]);
            bool ok = s.Length == expected && seam <= maxStep && Math.Abs(mean) < 1e-3;
            Console.WriteLine();
            Console.WriteLine($"BGM 루프: {BattleMusic.BarCount}마디, {BattleMusic.Bpm} BPM, {BattleMusic.LoopSeconds:F4}초, 샘플 {s.Length} (기대 {expected})");
            Console.WriteLine($"  이음매 |마지막−처음| = {seam:F5}  (안쪽 샘플 간 차이: 99% {p99:F5}, 최대 {maxStep:F5}),  직류 평균 = {mean:E2}");
            double[][] stems = BattleMusic.RenderStems(sr);
            var parts = new List<string>();
            for (int k = 0; k < stems.Length; k++)
            {
                double sum = 0;
                foreach (double x in stems[k]) sum += x * x;
                parts.Add($"{BattleMusic.StemNames[k]} {Db(Math.Sqrt(sum / stems[k].Length)):F1}dB");
            }
            Console.WriteLine($"  채널 RMS(정규화 전): {string.Join(", ", parts)}");
            Console.WriteLine(ok ? "  루프 검사: 통과" : "  루프 검사: 실패");
            return ok;
        }

        static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Assets")) && Directory.Exists(Path.Combine(dir.FullName, "Tools"))) return dir.FullName;
                dir = dir.Parent;
            }
            return Directory.GetCurrentDirectory();
        }

        static string Hash(float[] s)
        {
            var bytes = new byte[s.Length * 4];
            Buffer.BlockCopy(s, 0, bytes, 0, bytes.Length);
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        static double Db(double x) => x > 0 ? 20 * Math.Log10(x) : double.NegativeInfinity;

        static string ToSnake(string s) =>
            string.Concat(s.Select((ch, i) => i > 0 && char.IsUpper(ch) ? "_" + char.ToLowerInvariant(ch) : char.ToLowerInvariant(ch).ToString()));

        /// <summary>16bit PCM mono WAV.</summary>
        static void WriteWav(string path, float[] s, int sr)
        {
            using var fs = File.Create(path);
            using var w = new BinaryWriter(fs);
            int dataBytes = s.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + dataBytes); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(sr); w.Write(sr * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(dataBytes);
            foreach (float x in s)
            {
                double v = Math.Round(Math.Clamp(x, -1f, 1f) * 32767.0);
                w.Write((short)v);
            }
        }
    }
}
