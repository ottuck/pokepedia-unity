using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Pokepedia.App.Battle;
using Pokepedia.Content;
using Pokepedia.Domain;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    /// <summary>
    /// 실제 데이터(Assets/Content/Data)로 돌리는 밸런스 시뮬레이션 (PROJECT.md 6.5).
    /// Demo 매치업(피카츄 vs 거북왕)의 피카츄 승률 60~70%, 평균 4~6턴을 시드 고정으로 검증한다.
    /// </summary>
    public class BalanceTests
    {
        const int DemoPlayer = 25;  // 피카츄
        const int DemoNpc = 9;      // 거북왕
        const int Battles = 5000;
        const uint Seed = 20261016;

        static ContentCatalog LoadCatalog()
        {
            var starts = new[] { Directory.GetCurrentDirectory(), TestContext.CurrentContext.TestDirectory, AppContext.BaseDirectory };
            foreach (var start in starts)
                for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    var data = Path.Combine(dir.FullName, "Assets", "Content", "Data");
                    if (Directory.Exists(data))
                        return ContentLoader.Load(
                            File.ReadAllText(Path.Combine(data, "species.json")),
                            File.ReadAllText(Path.Combine(data, "moves.json")),
                            File.ReadAllText(Path.Combine(data, "roster.json")));
                }
            throw new DirectoryNotFoundException("Assets/Content/Data not found");
        }

        static SimulationResult Simulate(ContentCatalog c, int player, int npc, BattleRules rules, Func<Move, Move> tweak = null)
        {
            tweak ??= m => m;
            var p = c.GetSpecies(player).Species;
            var n = c.GetSpecies(npc).Species;
            var pm = c.MovesFor(player).Select(tweak).ToArray();
            var nm = c.MovesFor(npc).Select(tweak).ToArray();
            return BattleSimulator.Run(BattleSimulator.Factory(p, pm, rules), BattleSimulator.Factory(n, nm, rules), rules, Battles, Seed);
        }

        [Test]
        public void DemoMatchup_PikachuWinRate_IsSixtyToSeventyPercent()
        {
            var r = Simulate(LoadCatalog(), DemoPlayer, DemoNpc, BattleRules.Default);
            TestContext.WriteLine($"Pikachu vs Blastoise: win {r.PlayerWinRate:P1}, turns avg {r.AverageTurns:F2} (min {r.MinTurns}, max {r.MaxTurns})");
            Assert.That(r.PlayerWinRate, Is.InRange(0.60, 0.70));
        }

        [Test]
        public void DemoMatchup_LastsFourToSixTurnsOnAverage()
        {
            var r = Simulate(LoadCatalog(), DemoPlayer, DemoNpc, BattleRules.Default);
            Assert.That(r.AverageTurns, Is.InRange(4.0, 6.0));
        }

        /// <summary>
        /// 조정용 표: 압축 계수 K × 대표 기술 위력. 평소에는 돌지 않는다.
        /// dotnet test Tests.Dotnet --filter "FullyQualifiedName~BalanceTests.Sweep" --logger "console;verbosity=detailed"
        /// </summary>
        [Test, Explicit("밸런스 조정용 표 출력")]
        public void Sweep_CompressionAndSignaturePower()
        {
            var c = LoadCatalog();
            var sb = new StringBuilder("K\\sigPow");
            var powers = Env("POKE_SWEEP_POWERS", new[] { 28, 32, 36, 40, 44, 48 });
            foreach (var pw in powers) sb.Append($"\t{pw}");
            sb.AppendLine();
            foreach (var k in Env("POKE_SWEEP_KS", new[] { 4, 5, 6, 7, 8, 10 }).Select(x => x > 10 ? x / 100.0 : x / 10.0))
            {
                sb.Append(k.ToString("F2"));
                var rules = BattleRules.Default with { CompressionK = k };
                foreach (var pw in powers)
                {
                    var r = Simulate(c, DemoPlayer, DemoNpc, rules, m => m.Power == 16 ? m : m with { Power = pw });
                    sb.Append($"\t{r.PlayerWinRate:P0}/{r.AverageTurns:F1}");
                }
                sb.AppendLine();
            }
            TestContext.WriteLine(sb.ToString());
        }

        /// <summary>환경변수로 표 범위를 바꾼다 (예: POKE_SWEEP_KS="55,60,65" → K 0.55..., 단위 1/100 이면 값 ≥ 11)</summary>
        static int[] Env(string name, int[] fallback)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(v) ? fallback : v.Split(',').Select(s => int.Parse(s.Trim())).ToArray();
        }
    }
}
