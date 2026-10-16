using System;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.App.Battle
{
    public sealed record SimulationResult(int Battles, int PlayerWins, double AverageTurns, int MinTurns, int MaxTurns)
    {
        public double PlayerWinRate => Battles == 0 ? 0 : (double)PlayerWins / Battles;
    }

    /// <summary>
    /// 밸런스 조정용 시드 고정 시뮬레이션(PROJECT.md 6.5). 양쪽 모두 NpcBattleAi 로 행동한다
    /// (플레이어도 "가장 센 기술을 고르는 사람"으로 가정). 시드가 같으면 결과가 항상 같다.
    /// </summary>
    public static class BattleSimulator
    {
        /// <summary>한 판이 이 턴 수를 넘으면 규칙·데이터 오류로 보고 예외를 던진다(무한 루프 방지).</summary>
        public const int TurnLimit = 200;

        /// <param name="createPlayer">판마다 새 Battler 를 만든다 (HP 상태가 판 사이에 이어지지 않게)</param>
        public static SimulationResult Run(Func<Battler> createPlayer, Func<Battler> createNpc, BattleRules rules, int battles, uint seed)
        {
            int wins = 0, min = int.MaxValue, max = 0;
            long totalTurns = 0;
            for (int i = 0; i < battles; i++)
            {
                var state = new BattleState(new BattleSide(createPlayer()), new BattleSide(createNpc()));
                var engine = new BattleEngine(state, rules, new SeededRandom(unchecked(seed + (uint)i * 2654435761u) | 1u));
                engine.Start();
                while (!state.IsOver)
                {
                    if (state.Turn >= TurnLimit) throw new InvalidOperationException($"battle {i} exceeded {TurnLimit} turns");
                    var p = state.Sides[BattleState.Player].Active;
                    var n = state.Sides[BattleState.Npc].Active;
                    engine.PlayTurn(NpcBattleAi.Choose(p, n, rules), NpcBattleAi.Choose(n, p, rules));
                }
                if (state.Winner == BattleState.Player) wins++;
                totalTurns += state.Turn;
                min = Math.Min(min, state.Turn);
                max = Math.Max(max, state.Turn);
            }
            return new SimulationResult(battles, wins, battles == 0 ? 0 : (double)totalTurns / battles, battles == 0 ? 0 : min, max);
        }

        /// <summary>종족 데이터로 Battler 를 만드는 도우미</summary>
        public static Func<Battler> Factory(Species species, Move[] moves, BattleRules rules)
            => () => Battler.Create(species, moves, rules);
    }
}
