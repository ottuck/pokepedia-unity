using System.Linq;
using NUnit.Framework;
using Pokepedia.App.Battle;
using Pokepedia.Content;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    /// <summary>
    /// 실제 데이터(Assets/Content/Data)로 돌리는 밸런스 시뮬레이션 (PROJECT.md 6.5).
    /// Demo 매치업(피카츄 vs 거북왕)의 피카츄 승률 85~93%, 평균 4~6턴을 시드 고정으로 검증한다.
    /// (2026-10-17 사용자 피드백: 60~70%에서는 한 번만 빗나가도 지는 아슬아슬한 판이라 10만볼트 명중 100·하이드로펌프 명중 85로 조정)
    /// </summary>
    public class BalanceTests
    {
        [Test]
        public void DemoMatchup_PikachuWinsEightyFiveToNinetyThreePercent_InFourToSixTurns()
        {
            const int player = 25, npc = 9; // 피카츄, 거북왕
            var c = ContentLoader.Load(ContentDataFilesTests.Read("Data", "species.json"),
                ContentDataFilesTests.Read("Data", "moves.json"), ContentDataFilesTests.Read("Data", "roster.json"));
            var rules = BattleRules.Default;
            var r = BattleSimulator.Run(
                BattleSimulator.Factory(c.GetSpecies(player).Species, c.MovesFor(player).ToArray(), rules),
                BattleSimulator.Factory(c.GetSpecies(npc).Species, c.MovesFor(npc).ToArray(), rules),
                rules, 5000, 20261016);
            TestContext.WriteLine($"Pikachu vs Blastoise: win {r.PlayerWinRate:P1}, turns avg {r.AverageTurns:F2}");
            Assert.That(r.PlayerWinRate, Is.InRange(0.85, 0.93));
            Assert.That(r.AverageTurns, Is.InRange(4.0, 6.0));
        }
    }
}
