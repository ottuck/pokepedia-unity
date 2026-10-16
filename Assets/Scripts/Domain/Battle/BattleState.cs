using System;
using System.Collections.Generic;
using System.Linq;

namespace Pokepedia.Domain.Battle
{
    /// <summary>
    /// 한쪽 진영. 지금은 1:1 이지만 3:3(파티) 확장을 위해 처음부터 파티 + 현재 출전 인덱스 구조로 둔다(PROJECT.md 5.4, 7.2).
    /// </summary>
    public sealed class BattleSide
    {
        public IReadOnlyList<Battler> Party { get; }
        public int ActiveIndex { get; }
        public Battler Active => Party[ActiveIndex];
        public bool AllFainted => Party.All(b => b.Fainted);

        public BattleSide(IReadOnlyList<Battler> party, int activeIndex = 0)
        {
            if (party == null || party.Count == 0) throw new ArgumentException("party must not be empty", nameof(party));
            Party = party.ToArray();
            ActiveIndex = activeIndex;
        }

        public BattleSide(Battler single) : this(new[] { single }) { }
    }

    /// <summary>배틀 전체 상태. 브라우저 탭 메모리에만 존재한다(PROJECT.md 10.1).</summary>
    public sealed class BattleState
    {
        public const int Player = 0;
        public const int Npc = 1;

        public IReadOnlyList<BattleSide> Sides { get; }
        public int Turn { get; internal set; }
        public int? Winner { get; internal set; }
        public bool IsOver => Winner.HasValue;

        public BattleState(BattleSide player, BattleSide npc)
        {
            Sides = new[] { player, npc };
        }

        public static int Opponent(int side) => 1 - side;
    }

    /// <summary>한 턴에 한쪽이 고르는 행동. 지금은 기술 사용뿐이다(도망은 Application 흐름에서 처리).</summary>
    public abstract record BattleAction;

    /// <summary>MoveIndex 는 Battler.Moves 의 인덱스</summary>
    public sealed record UseMove(int MoveIndex) : BattleAction;
}
