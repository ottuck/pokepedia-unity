using NUnit.Framework;
using Pokepedia.App.Battle;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    public class BattleSimulatorTests
    {
        [Test]
        public void MirrorMatch_IsRoughlyEven()
        {
            // 같은 능력치끼리는 스피드 동률(무작위 선공) → 승률이 반반 근처. 한쪽으로 쏠리면 시뮬레이터나 엔진에 편향이 있다.
            static Battler Mon() => TestMon.Battler(TestMon.Stats(hp: 120, atk: 60, spe: 50), PokemonType.Fire, TestMove.Tackle);
            var r = BattleSimulator.Run(Mon, Mon, BattleRules.Default, 2000, 99);
            Assert.That(r.PlayerWinRate, Is.InRange(0.44, 0.56));
        }
    }
}
