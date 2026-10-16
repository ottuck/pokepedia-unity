using System;
using System.Collections.Generic;
using Pokepedia.Domain;

namespace Pokepedia.Content
{
    /// <summary>울음소리 합성 인자: 반음 오프셋(체급이 클수록 낮게), 길이 배수 (PROJECT.md 8.3)</summary>
    public sealed record CryParams(double Semitones, double Length);

    /// <summary>
    /// 종(species.json 의 animProfile 키)별 연출 데이터 (PROJECT.md 5.2: 포켓몬별 애니메이션을 만들지 않고 강도·속도를 데이터로 조정).
    /// MoveFx: 기술 id → 전용 이펙트 id (Showcase 오버라이드, 예: pikachu 의 thunderbolt → "lightning_strike").
    /// 없으면 화면이 기술의 animKind(접촉/발사/효과) 기본 타임라인을 쓴다.
    /// </summary>
    public sealed record AnimationProfile(
        string Key,
        CryParams Cry,
        double WindupSec,
        int LungePx,
        int HitShakePx,
        int KnockbackPx,
        IReadOnlyDictionary<string, string> MoveFx);

    /// <summary>타입별 이펙트 색 (0xRRGGBB). 공통 이펙트 프리팹 하나 + 타입별 색 파라미터 (PROJECT.md 5.2).</summary>
    public sealed record TypeVisual(PokemonType Type, int PrimaryRgb, int SecondaryRgb);

    /// <summary>Assets/Content/Data/animation.json 로더. 순수 C#.</summary>
    public sealed class AnimationCatalog
    {
        public static AnimationCatalog Parse(string json) => throw new NotImplementedException();

        /// <summary>프로필 키가 없을 때 쓰는 기본값</summary>
        public AnimationProfile Defaults => throw new NotImplementedException();

        /// <summary>key 의 프로필(빠진 필드는 기본값으로 채움). 모르는 키면 기본값을 key 이름으로 돌려준다.</summary>
        public AnimationProfile ProfileFor(string key) => throw new NotImplementedException();

        /// <summary>18타입 모두 정의되어 있어야 한다.</summary>
        public TypeVisual TypeFor(PokemonType type) => throw new NotImplementedException();

        /// <summary>전용 이펙트 id, 없으면 null</summary>
        public string MoveFxFor(string profileKey, string moveId) => throw new NotImplementedException();
    }
}
