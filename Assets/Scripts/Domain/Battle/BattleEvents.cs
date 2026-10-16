namespace Pokepedia.Domain.Battle
{
    // 엔진 → 화면 출력. 엔진은 문자열을 만들지 않고 이 이벤트 목록만 내보낸다(PROJECT.md 7.3).
    // 화면(Presentation)은 이벤트 → 로케일 키 → 문장 순으로 메시지를 조립하고 연출을 재생한다.
    // Side: 0 = 플레이어, 1 = NPC.
    // Java 비교: sealed interface BattleEvent permits ... + record 들과 같은 구조. switch 식으로 분기한다.

    public abstract record BattleEvent;

    /// <summary>포켓몬이 나왔다 ("야생의 ○○이(가) 나타났다!" / "가라! ○○!")</summary>
    public sealed record PokemonSentOut(int Side, int SpeciesId, int Hp, int MaxHp) : BattleEvent;

    /// <summary>새 턴 시작 (1부터)</summary>
    public sealed record TurnStarted(int Turn) : BattleEvent;

    /// <summary>"○○의 ○○!"</summary>
    public sealed record MoveUsed(int Side, string MoveId) : BattleEvent;

    /// <summary>"공격은 빗나갔다!"</summary>
    public sealed record MoveMissed(int Side, string MoveId) : BattleEvent;

    /// <summary>
    /// 피해. TargetSide 는 맞은 쪽. Effectiveness 로 "효과가 굉장했다!" 등을, Critical 로 "급소에 맞았다!" 를 고른다.
    /// Effectiveness 가 0 이면 Amount 도 0 ("효과가 없는 것 같다…").
    /// </summary>
    public sealed record DamageDealt(int TargetSide, int Amount, int HpAfter, double Effectiveness, bool Critical) : BattleEvent;

    /// <summary>"○○은(는) 쓰러졌다!"</summary>
    public sealed record Fainted(int Side) : BattleEvent;

    public sealed record BattleEnded(int WinnerSide) : BattleEvent;
}
