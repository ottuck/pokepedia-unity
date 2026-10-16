using Pokepedia.Domain;

namespace Pokepedia.Content
{
    /// <summary>공격 연출 타임라인 종류 (PROJECT.md 6.3): 접촉형 / 발사형 / 효과형.</summary>
    public enum AnimKind { Contact, Projectile, Effect }

    /// <summary>
    /// 기술의 역할. Basic = 모든 포켓몬이 받는 기본기(데이터 전체에 정확히 1개),
    /// Signature = 그 기술 타입의 대표 기술(타입마다 최대 1개). 종의 기술은 이 역할로 자동 배정한다.
    /// </summary>
    public enum MoveRole { Basic, Signature }

    /// <summary>roster.json 의 품질 등급 (PROJECT.md 5.2).</summary>
    public enum RosterTier { Showcase, Standard }

    /// <summary>스프라이트 시트 이름(확장자 없음). Tools/sprites 의 출력 규칙 {side}_{id}[_shiny]_sheet 을 따른다.</summary>
    public sealed record SpriteSet(string Front, string Back, string FrontShiny, string BackShiny);

    /// <summary>엔진용 Species + 화면용 정보(이름 키·스프라이트·연출 프로필·전설 여부).</summary>
    public sealed record SpeciesInfo(Species Species, string NameKey, SpriteSet Sprites, string AnimProfile, bool Legendary)
    {
        public int Id => Species.Id;
    }

    /// <summary>엔진용 Move + 화면용 정보(이름 키·연출 종류)와 자동 배정용 역할.</summary>
    public sealed record MoveInfo(Move Move, string NameKey, AnimKind AnimKind, MoveRole Role)
    {
        public string Id => Move.Id;
    }

    /// <summary>
    /// 지원 포켓몬 한 줄. NpcWeight 는 Collection 모드 NPC 가중 추첨의 상대 가중치(0 이면 NPC 로 나오지 않음, PROJECT.md 6.5).
    /// </summary>
    public sealed record RosterEntry(int Id, RosterTier Tier, int NpcWeight);
}
