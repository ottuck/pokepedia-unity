using NUnit.Framework;
using Pokepedia.App.Battle;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Tests
{
    public class BattleSimulatorTests
    {
        static readonly BattleRules Rules = BattleRules.Default;

        static Battler Mon(int hp, int atk, int spe)
            => TestMon.Battler(TestMon.Stats(hp: hp, atk: atk, spe: spe), PokemonType.Fire, TestMove.Tackle);

        [Test]
        public void SameSeed_SameResult()
        {
            var a = BattleSimulator.Run(() => Mon(120, 60, 50), () => Mon(120, 60, 50), Rules, 200, 42);
            var b = BattleSimulator.Run(() => Mon(120, 60, 50), () => Mon(120, 60, 50), Rules, 200, 42);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void ClearlyStrongerSide_WinsAlmostAlways()
        {
            var r = BattleSimulator.Run(() => Mon(200, 120, 90), () => Mon(100, 40, 30), Rules, 300, 7);
            Assert.That(r.PlayerWinRate, Is.GreaterThan(0.95));
            Assert.That(r.MinTurns, Is.GreaterThanOrEqualTo(1));
            Assert.That(r.MaxTurns, Is.LessThan(BattleSimulator.TurnLimit));
        }

        [Test]
        public void MirrorMatch_IsRoughlyEven()
        {
            // 같은 능력치끼리는 스피드 동률(무작위 선공) → 승률이 반반 근처
            var r = BattleSimulator.Run(() => Mon(120, 60, 50), () => Mon(120, 60, 50), Rules, 2000, 99);
            Assert.That(r.PlayerWinRate, Is.InRange(0.44, 0.56));
        }
    }
}
