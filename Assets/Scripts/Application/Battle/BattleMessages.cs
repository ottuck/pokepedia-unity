using System;
using System.Collections.Generic;
using Pokepedia.Domain.Battle;

namespace Pokepedia.App.Battle
{
    /// <summary>
    /// 로케일 키 + 인자. 화면이 LocaleTable.Format(Key, Args) 로 문장을 만든다.
    /// 엔진과 Application 은 문장을 만들지 않는다(PROJECT.md 7.3: 이벤트 → 로케일 키 → 문자열).
    /// </summary>
    public sealed record MessageLine(string Key, IReadOnlyList<string> Args)
    {
        public static MessageLine Of(string key, params string[] args) => new MessageLine(key, args);
    }

    /// <summary>이름 조회: 종 id → 표시 이름, 기술 id → 표시 이름 (화면이 로케일로 채워 넘긴다)</summary>
    public sealed class BattleNames
    {
        public Func<int, string> Species { get; }
        public Func<string, string> Move { get; }

        public BattleNames(Func<int, string> species, Func<string, string> move)
        {
            Species = species ?? throw new ArgumentNullException(nameof(species));
            Move = move ?? throw new ArgumentNullException(nameof(move));
        }
    }

    /// <summary>
    /// 배틀 이벤트 → 메시지 줄 (PROJECT.md 5.3 "턴 처리 메시지").
    /// 이벤트 하나가 0~2줄을 낸다 (예: DamageDealt → "급소에 맞았다!" + "효과가 굉장했다!").
    /// </summary>
    public static class BattleMessages
    {
        /// <param name="speciesIdBySide">Side(0 플레이어, 1 NPC) → 현재 종 id</param>
        public static IReadOnlyList<MessageLine> For(BattleEvent e, BattleNames names, Func<int, int> speciesIdBySide)
        {
            throw new NotImplementedException();
        }
    }
}
