using System.Linq;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.App.Battle
{
    /// <summary>
    /// NPC AI (난이도 하나, PROJECT.md 5.3·6.4): 예상 데미지(위력·명중률·자속·상성 반영)로 기술 순위를 매긴다(같으면 앞쪽 기술).
    /// 같은 기술만 쓰면 단조로우므로 턴에 따라 섞는다: 턴 1·3 은 1순위, 2 는 2순위, 4 는 3순위, 이후 4턴마다 반복.
    /// 난수를 쓰지 않으므로 같은 상황·같은 턴이면 항상 같은 선택을 한다.
    /// </summary>
    public static class NpcBattleAi
    {
        static readonly int[] Rotation = { 0, 1, 0, 2 };

        /// <param name="turn">이번에 진행할 턴(1부터). 생략하면 1 = 항상 가장 센 기술.</param>
        public static UseMove Choose(Battler self, Battler target, BattleRules rules, int turn = 1)
        {
            var ranked = Enumerable.Range(0, self.Moves.Count)
                .OrderByDescending(i => DamageCalculator.Expected(self, target, self.Moves[i], rules))
                .ThenBy(i => i)
                .ToArray();
            int rank = Rotation[(System.Math.Max(turn, 1) - 1) % Rotation.Length];
            return new UseMove(ranked[System.Math.Min(rank, ranked.Length - 1)]);
        }
    }
}
