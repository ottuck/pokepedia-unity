using System;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;

namespace Pokepedia.App.Battle
{
    /// <summary>
    /// NPC AI (난이도 하나, PROJECT.md 5.3·6.4): 예상 데미지(위력·명중률·자속·상성 반영)가 가장 큰 기술을 고른다.
    /// 같으면 앞쪽 기술. 난수를 쓰지 않으므로 같은 상황이면 항상 같은 선택을 한다.
    /// </summary>
    public static class NpcBattleAi
    {
        public static UseMove Choose(Battler self, Battler target, BattleRules rules)
        {
            throw new NotImplementedException();
        }
    }
}
