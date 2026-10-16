using System;
using System.Collections.Generic;
using System.Linq;
using Pokepedia.Content;
using Pokepedia.Domain;
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
    /// 배틀 화면의 로케일 키 (ko/en/ja.json 의 "battle", "types"). 실제 파일에 모두 있는지는 BattleMessagesTests 가 검사한다.
    /// NPC 쪽은 "야생의 ○○" 처럼 따로 키를 둔다: 원작이 상대 포켓몬을 "야생의"/"やせいの"/"The wild" 로 구분하고,
    /// 영어는 문장 앞("The wild X used…")과 가운데의 대소문자가 달라 이름 앞에 접두어를 붙이는 방식으로는 번역할 수 없다.
    /// </summary>
    public static class BattleTextKeys
    {
        // ----- 메시지 (BattleMessages 가 낸다) — 인자 -----
        /// <summary>{0} 상대 종 이름: "야생의 {0}이(가) 나타났다!"</summary>
        public const string WildAppeared = "battle.wild_appeared";
        /// <summary>{0} 플레이어 종 이름: "가라! {0}!"</summary>
        public const string PlayerSentOut = "battle.player_sent_out";
        /// <summary>{0} 사용자, {1} 기술: "{0}의 {1}!"</summary>
        public const string PlayerMove = "battle.player_move";
        /// <summary>{0} 사용자, {1} 기술: "야생의 {0}의 {1}!"</summary>
        public const string WildMove = "battle.wild_move";
        public const string Missed = "battle.missed";
        public const string CriticalHit = "battle.critical_hit";
        public const string SuperEffective = "battle.super_effective";
        public const string NotVeryEffective = "battle.not_very_effective";
        public const string NoEffect = "battle.no_effect";
        /// <summary>{0} 플레이어 종 이름</summary>
        public const string PlayerFainted = "battle.player_fainted";
        /// <summary>{0} 상대 종 이름</summary>
        public const string WildFainted = "battle.wild_fainted";
        public const string Won = "battle.won";
        public const string Lost = "battle.lost";
        public const string Escaped = "battle.escaped";
        public const string CannotEscape = "battle.cannot_escape";

        // ----- 화면 문구 (인자 없음) -----
        public const string MenuFight = "battle.menu.fight";
        public const string MenuRun = "battle.menu.run";
        public const string ResultNextOpponent = "battle.result.next_opponent";
        public const string ResultRetry = "battle.result.retry";
        public const string ResultTitle = "battle.result.title";
        public const string InfoType = "battle.info.type";
        public const string InfoPower = "battle.info.power";
        public const string InfoAccuracy = "battle.info.accuracy";

        /// <summary>타입 이름 키: "types." + 소문자 타입명 (moves.json 의 type 값과 같은 표기)</summary>
        public static string TypeName(PokemonType type) => "types." + ContentLoader.ToJsonName(type);

        /// <summary>BattleMessages.For / ForRun 이 낼 수 있는 키 전부</summary>
        public static IReadOnlyList<string> MessageKeys { get; } = new[]
        {
            WildAppeared, PlayerSentOut, PlayerMove, WildMove, Missed, CriticalHit, SuperEffective, NotVeryEffective, NoEffect,
            PlayerFainted, WildFainted, Won, Lost, Escaped, CannotEscape,
        };

        /// <summary>메뉴·결과·기술 정보 라벨과 18개 타입 이름</summary>
        public static IReadOnlyList<string> UiKeys { get; } = new[]
            {
                MenuFight, MenuRun, ResultNextOpponent, ResultRetry, ResultTitle, InfoType, InfoPower, InfoAccuracy,
            }
            .Concat(((PokemonType[])Enum.GetValues(typeof(PokemonType))).Select(TypeName))
            .ToArray();

        public static IReadOnlyList<string> All { get; } = MessageKeys.Concat(UiKeys).ToArray();
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
            if (e == null) throw new ArgumentNullException(nameof(e));
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (speciesIdBySide == null) throw new ArgumentNullException(nameof(speciesIdBySide));

            string NameOf(int side) => names.Species(speciesIdBySide(side));
            bool IsNpc(int side) => side == BattleState.Npc;

            switch (e)
            {
                case PokemonSentOut s:
                    return One(IsNpc(s.Side) ? BattleTextKeys.WildAppeared : BattleTextKeys.PlayerSentOut, names.Species(s.SpeciesId));
                case TurnStarted _:
                    return None;
                case MoveUsed m:
                    return One(IsNpc(m.Side) ? BattleTextKeys.WildMove : BattleTextKeys.PlayerMove, NameOf(m.Side), names.Move(m.MoveId));
                case MoveMissed _:
                    return One(BattleTextKeys.Missed);
                case DamageDealt d:
                {
                    var lines = new List<MessageLine>(2);
                    if (d.Critical) lines.Add(MessageLine.Of(BattleTextKeys.CriticalHit));
                    if (d.Effectiveness <= 0) lines.Add(MessageLine.Of(BattleTextKeys.NoEffect));
                    else if (d.Effectiveness > 1) lines.Add(MessageLine.Of(BattleTextKeys.SuperEffective));
                    else if (d.Effectiveness < 1) lines.Add(MessageLine.Of(BattleTextKeys.NotVeryEffective));
                    return lines;
                }
                case Fainted f:
                    return One(IsNpc(f.Side) ? BattleTextKeys.WildFainted : BattleTextKeys.PlayerFainted, NameOf(f.Side));
                case BattleEnded b:
                    return One(b.WinnerSide == BattleState.Player ? BattleTextKeys.Won : BattleTextKeys.Lost);
                default:
                    throw new NotSupportedException($"no message for {e.GetType().Name}");
            }
        }

        /// <summary>도망 결과 → "무사히 도망쳤다!" / "도망칠 수 없다!"</summary>
        public static MessageLine ForRun(RunOutcome outcome)
            => MessageLine.Of(outcome == RunOutcome.Escaped ? BattleTextKeys.Escaped : BattleTextKeys.CannotEscape);

        static readonly IReadOnlyList<MessageLine> None = Array.Empty<MessageLine>();

        static IReadOnlyList<MessageLine> One(string key, params string[] args) => new[] { MessageLine.Of(key, args) };
    }
}
