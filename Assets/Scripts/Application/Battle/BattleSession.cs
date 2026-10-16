using System;
using System.Collections.Generic;
using Pokepedia.Content;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.App.Battle
{
    /// <summary>Demo = 피카츄 vs 거북왕 고정(도망 불가), Collection = 보유 ∩ 지원 (PROJECT.md 9.2)</summary>
    public enum BattleMode { Demo, Collection }

    public enum RunOutcome
    {
        /// <summary>"도망칠 수 없다!" (Demo) — 같은 배틀이 이어진다</summary>
        CannotEscape,
        /// <summary>"무사히 도망쳤다!" (Collection) — 호출자가 NextBattle() 로 새 상대를 부른다</summary>
        Escaped,
    }

    /// <summary>
    /// 배틀 흐름(PROJECT.md 5.3, 7.2 Application): 플레이어 행동 접수 → NPC 행동 결정(NpcBattleAi) → 엔진 호출 → 이벤트 전달,
    /// 그리고 승리 후 다음 상대 / 패배 후 다시 도전 / 도망. 화면은 이 클래스가 돌려주는 이벤트를 재생만 한다.
    /// 한 판마다 BattleEngine 을 새로 만든다 (플레이어 HP 도 가득 찬 상태로 다시 시작).
    /// </summary>
    public sealed class BattleSession
    {
        public BattleSession(ContentCatalog catalog, BattleRules rules, IRandom random, BattleMode mode,
                             int playerSpeciesId, Func<int> pickOpponent)
        {
            throw new NotImplementedException();
        }

        public BattleMode Mode => throw new NotImplementedException();

        /// <summary>현재 배틀의 엔진 상태 (화면이 이름·HP·기술 목록을 읽는다)</summary>
        public BattleState State => throw new NotImplementedException();

        public Battler Player => throw new NotImplementedException();
        public Battler Npc => throw new NotImplementedException();

        /// <summary>기술 정보(이름 키·animKind 포함). 인덱스는 Player.Moves 와 같다.</summary>
        public IReadOnlyList<MoveInfo> PlayerMoveInfos => throw new NotImplementedException();

        /// <summary>첫 배틀 시작: 등장 이벤트</summary>
        public IReadOnlyList<BattleEvent> Start() => throw new NotImplementedException();

        /// <summary>플레이어가 기술을 골랐다. NPC 는 AI 가 고르고 한 턴을 진행한다.</summary>
        public IReadOnlyList<BattleEvent> UseMove(int moveIndex) => throw new NotImplementedException();

        public RunOutcome TryRun() => throw new NotImplementedException();

        /// <summary>새 상대(pickOpponent)로 새 배틀. 승리 후 [다음 상대와 싸운다], 도망 성공 후 호출.</summary>
        public IReadOnlyList<BattleEvent> NextBattle() => throw new NotImplementedException();

        /// <summary>같은 상대로 처음부터 다시. 패배 후 [다시 도전].</summary>
        public IReadOnlyList<BattleEvent> Retry() => throw new NotImplementedException();
    }
}
