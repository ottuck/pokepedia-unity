using System.Collections.Generic;

namespace Pokepedia.Domain
{
    /// <summary>현대 기준 18타입 (PROJECT.md 6.1). 순서는 상성표의 행·열 순서와 같다.</summary>
    public enum PokemonType
    {
        Normal, Fire, Water, Electric, Grass, Ice, Fighting, Poison, Ground,
        Flying, Psychic, Bug, Rock, Ghost, Dragon, Dark, Steel, Fairy,
    }

    /// <summary>물리 기술은 공격÷방어, 특수 기술은 특공÷특방으로 계산한다.</summary>
    public enum MoveCategory { Physical, Special }

    /// <summary>
    /// 능력치 6종. 종족값(base stat)과 실제 능력치(레벨 50 환산 후) 모두 이 타입으로 표현한다.
    /// Java 의 record 와 같다: 값이 같으면 Equals 가 true.
    /// </summary>
    public sealed record StatBlock(int Hp, int Attack, int Defense, int SpAttack, int SpDefense, int Speed);

    /// <summary>
    /// 종(種). 엔진이 아는 것은 ID·타입·종족값뿐이다. 이름·스프라이트는 Content/Presentation 이 nameKey 로 찾는다.
    /// Types 는 1~2개(이중 타입).
    /// </summary>
    public sealed record Species(int Id, IReadOnlyList<PokemonType> Types, StatBlock BaseStats);

    /// <summary>
    /// 기술. 이름은 장식이고 규칙은 타입·분류·위력·명중률이 결정한다(PROJECT.md 6.1).
    /// Accuracy 는 % (100 이면 항상 명중, 난수를 쓰지 않는다).
    /// </summary>
    public sealed record Move(string Id, PokemonType Type, MoveCategory Category, int Power, int Accuracy);
}
