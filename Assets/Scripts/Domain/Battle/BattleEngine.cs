using System;
using System.Collections.Generic;
using Pokepedia.Domain.Rules;

namespace Pokepedia.Domain.Battle
{
    /// <summary>
    /// 턴제 배틀 엔진. 입력은 양쪽의 BattleAction, 출력은 BattleEvent 목록이다.
    /// 상태(BattleState)를 직접 바꾸며, 난수는 모두 생성자로 받은 IRandom 에서만 뽑는다.
    /// </summary>
    public sealed class BattleEngine
    {
        readonly BattleRules rules;
        readonly IRandom random;

        public BattleState State { get; }

        public BattleEngine(BattleState state, BattleRules rules, IRandom random)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>배틀 시작: 양쪽의 첫 포켓몬이 나온다.</summary>
        public IReadOnlyList<BattleEvent> Start()
        {
            throw new NotImplementedException();
        }

        /// <summary>한 턴 진행. actions[0] = 플레이어, actions[1] = NPC.</summary>
        public IReadOnlyList<BattleEvent> PlayTurn(BattleAction playerAction, BattleAction npcAction)
        {
            throw new NotImplementedException();
        }
    }
}
