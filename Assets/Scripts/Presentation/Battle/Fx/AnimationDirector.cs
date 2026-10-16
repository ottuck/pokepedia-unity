using System;
using System.Collections;
using System.Linq;
using Pokepedia.Content;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Presentation.Audio;
using Pokepedia.Presentation.Audio.Synth;
using UnityEngine;

namespace Pokepedia.Presentation.Battle.Fx
{
    /// <summary>
    /// 연출 디렉터 (PROJECT.md 5.2, 7.3): 기본 타임라인(접촉 / 발사 / 효과) + 종(AnimationProfile)·기술(MoveFx) 오버라이드.
    /// Showcase 와 Standard 를 별도 시스템으로 만들지 않는다 — 같은 타임라인에 데이터만 다르게 넣는다.
    /// 규칙은 판단하지 않는다: 명중·급소·피해량은 이벤트(DamageDealt / MoveMissed)에 이미 정해져 있다.
    /// </summary>
    public sealed class AnimationDirector
    {
        readonly MonoBehaviour host;
        readonly BattleSideView[] sides;
        readonly VfxLayer vfx;
        readonly SoundBank sound;
        readonly AnimationCatalog anim;
        readonly ContentCatalog catalog;

        public AnimationDirector(MonoBehaviour host, BattleSideView[] sides, VfxLayer vfx, SoundBank sound,
                                 AnimationCatalog anim, ContentCatalog catalog)
        {
            this.host = host; this.sides = sides; this.vfx = vfx; this.sound = sound; this.anim = anim; this.catalog = catalog;
        }

        AnimationProfile ProfileOf(int speciesId) => anim.ProfileFor(catalog.GetSpecies(speciesId).AnimProfile);

        static Color Rgb(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        // ------------------------------------------------------------------ 등장 · 기절 · 승리
        public IEnumerator SendOut(int side, SpeciesInfo info)
        {
            var view = sides[side];
            view.Animator.Play(SpriteSheet.Load(side == BattleState.Player ? info.Sprites.Back : info.Sprites.Front));
            view.SetAlpha(1);
            view.SetFlash(0);
            // NPC 는 오른쪽에서, 플레이어는 왼쪽에서 미끄러져 들어온다
            var from = new Vector2(side == BattleState.Npc ? 150 : -150, 0);
            yield return Move(view, from, Vector2.zero, 0.35f);
            Cry(info);
            // 등장 직후 한 번 번쩍임
            view.SetFlash(0.8f); yield return VfxLayer.Wait(0.06f); view.SetFlash(0);
        }

        public void Cry(SpeciesInfo info)
        {
            var types = info.Species.Types.Select(ContentLoader.ToJsonName).Where(TypeCries.IsSupported).ToArray();
            if (types.Length == 0) return;
            var p = anim.ProfileFor(info.AnimProfile).Cry;
            sound.Cry(types, p.Semitones, p.Length);
        }

        public IEnumerator Faint(int side, SpeciesInfo info)
        {
            var view = sides[side];
            Cry(info); // 쓰러지기 전 울음 (원작 관례)
            yield return VfxLayer.Wait(0.35f);
            sound.Play(SfxKind.Faint);
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                float k = t / 0.45f;
                view.SetOffset(new Vector2(0, 40 * k * k));
                view.SetAlpha(1 - k);
                yield return null;
            }
            view.Image.enabled = false;
            view.SetOffset(Vector2.zero);
            view.SetAlpha(1);
        }

        public IEnumerator Victory(int side)
        {
            var view = sides[side];
            for (int i = 0; i < 2; i++)
            {
                yield return Move(view, Vector2.zero, new Vector2(0, -8), 0.1f);
                yield return Move(view, new Vector2(0, -8), Vector2.zero, 0.12f);
            }
        }

        // ------------------------------------------------------------------ 기술
        /// <param name="hit">명중했으면 피해 이벤트, 빗나갔으면 null</param>
        /// <param name="hpAnimation">HP 바를 줄이는 코루틴 (화면이 넘긴다)</param>
        public IEnumerator UseMove(int side, int attackerSpeciesId, int targetSpeciesId, MoveInfo move, DamageDealt hit, Func<IEnumerator> hpAnimation)
        {
            var attacker = sides[side];
            var target = sides[BattleState.Opponent(side)];
            var prof = ProfileOf(attackerSpeciesId);
            var targetProf = ProfileOf(targetSpeciesId);
            var tv = anim.TypeFor(move.Move.Type);
            Color c1 = Rgb(tv.PrimaryRgb), c2 = Rgb(tv.SecondaryRgb);
            var typeName = ContentLoader.ToJsonName(move.Move.Type);
            bool missed = hit == null;

            // 기술 소리: 속성 레시피(울음소리와 같은 합성기, 원래 "속성 공격음" 으로 만든 소리). 노말은 레시피가 없어 타격음만.
            if (TypeCries.IsSupported(typeName)) sound.Cry(new[] { typeName }, 0, 0.8);

            var fx = anim.MoveFxFor(prof.Key, move.Move.Id) ?? move.AnimKind switch
            {
                AnimKind.Contact => "contact",
                AnimKind.Projectile => "projectile",
                _ => "effect",
            };

            Vector2 toTarget = (target.Feet - attacker.Feet).normalized;
            switch (fx)
            {
                case "lightning_strike":
                    yield return Charge(attacker, c1, c2, prof.WindupSec, 26);
                    host.StartCoroutine(vfx.ScreenFlash(new Color(1f, 1f, 0.7f, 0.55f), 0.18f));
                    if (missed)
                    {
                        var aside = target.Center + new Vector2(28, 0);
                        yield return Parallel(vfx.Bolt(new Vector2(aside.x, 0), aside, Color.white, c1, 2, 0.3f), Dodge(target));
                        vfx.Burst(aside, c1, c2, 14, 20, 60, 0.35f, 2);
                    }
                    else
                    {
                        yield return vfx.Bolt(new Vector2(target.Center.x, 0), target.Center, Color.white, c1, 3, 0.36f);
                        vfx.Burst(target.Center, c1, c2, 40, 30, 110, 0.5f, 3, 0, 6);
                    }
                    break;

                case "water_cannon":
                    yield return Recoil(attacker, -toTarget * 5, prof.WindupSec);
                    yield return vfx.Stream(attacker.Center, missed ? target.Center + new Vector2(30, -10) : target.Center,
                                            c1, c2, 0.5f, 6, 260, 4, 3);
                    if (missed) yield return Dodge(target);
                    else vfx.Burst(target.Center, c1, c2, 36, 40, 120, 0.55f, 3, 260, 4);
                    break;

                case "contact":
                    yield return Charge(attacker, c1, c2, prof.WindupSec * 0.5f, 0);
                    var reach = toTarget * Mathf.Max(prof.LungePx, 1) * 3; // 상대 쪽으로 크게 돌진
                    if (missed)
                    {
                        yield return Parallel(Move(attacker, Vector2.zero, reach, 0.12f), Dodge(target));
                        yield return Move(attacker, reach, Vector2.zero, 0.15f);
                    }
                    else
                    {
                        yield return Move(attacker, Vector2.zero, reach, 0.1f);
                        vfx.Burst(target.Center, Color.white, c1, 18, 30, 90, 0.3f, 3);
                        yield return Move(attacker, reach, Vector2.zero, 0.15f);
                    }
                    break;

                case "projectile":
                    yield return Charge(attacker, c1, c2, prof.WindupSec, 10);
                    var dest = missed ? target.Center + new Vector2(36, 0) : target.Center;
                    if (missed) yield return Parallel(vfx.Orb(attacker.Center, dest, c1, 10, 0.35f, c2), Dodge(target));
                    else
                    {
                        yield return vfx.Orb(attacker.Center, dest, c1, 10, 0.35f, c2);
                        vfx.Burst(dest, c1, c2, 24, 30, 90, 0.4f, 3);
                    }
                    break;

                default: // effect
                    yield return Charge(attacker, c1, c2, prof.WindupSec, 10);
                    if (missed) yield return Dodge(target);
                    else
                    {
                        yield return vfx.Rings(target.Center, c1, 3, 0.5f);
                        vfx.Burst(target.Center, c1, c2, 20, 10, 50, 0.6f, 3, 0, 10);
                    }
                    break;
            }

            if (missed)
            {
                sound.Play(SfxKind.Miss);
                yield break;
            }
            yield return HitReaction(target, targetProf, hit, hpAnimation);
        }

        IEnumerator HitReaction(BattleSideView target, AnimationProfile targetProf, DamageDealt hit, Func<IEnumerator> hpAnimation)
        {
            if (hit.Amount <= 0)
            {
                // 효과가 없다: 반응 없이 HP 그대로
                if (hpAnimation != null) yield return hpAnimation();
                yield break;
            }
            sound.Play(hit.Critical ? SfxKind.CriticalHit : SfxKind.Hit);
            if (hit.Critical)
            {
                host.StartCoroutine(vfx.ScreenFlash(new Color(1, 1, 1, 0.9f), 0.25f));
                host.StartCoroutine(vfx.Shake(4, 0.3f));
            }
            // 넉백(종 데이터) + 흔들림 + 셰이더 번쩍임 3회
            var away = (target.Feet - sides[BattleState.Opponent(SideOf(target))].Feet).normalized;
            int knock = targetProf.KnockbackPx + (hit.Critical ? 4 : 0);
            int shake = targetProf.HitShakePx + (hit.Critical ? 2 : 0);
            for (int i = 0; i < 3; i++)
            {
                target.SetFlash(1);
                target.SetOffset(away * knock * (i == 0 ? 1f : 0.5f) + new Vector2(i % 2 == 0 ? shake : -shake, 0));
                yield return VfxLayer.Wait(0.06f);
                target.SetFlash(0);
                target.SetOffset(away * knock * 0.5f * (2 - i) / 2f);
                yield return VfxLayer.Wait(0.06f);
            }
            target.SetOffset(Vector2.zero);
            if (hpAnimation != null) yield return hpAnimation();
        }

        int SideOf(BattleSideView v) => v == sides[BattleState.Player] ? BattleState.Player : BattleState.Npc;

        // ------------------------------------------------------------------ 작은 동작들
        /// <summary>기 모으기: 몸이 살짝 떨리고 주변에 입자가 모인다(sparks = 입자 수, 0 이면 생략)</summary>
        IEnumerator Charge(BattleSideView view, Color c1, Color c2, double seconds, int sparks)
        {
            float total = (float)Math.Max(0.05, seconds);
            float nextSpark = 0;
            for (float t = 0; t < total; t += Time.deltaTime)
            {
                view.SetOffset(new Vector2(((int)(t * 30)) % 2 == 0 ? 1 : -1, 0));
                if (sparks > 0 && t >= nextSpark)
                {
                    vfx.Burst(view.Center + UnityEngine.Random.insideUnitCircle * (view.Size.x * 0.45f), c2, c1, Mathf.Max(1, sparks / 8), 5, 25, 0.25f, 2);
                    nextSpark += 0.05f;
                }
                view.SetFlash(Mathf.PingPong(t * 4, 0.5f));
                yield return null;
            }
            view.SetFlash(0);
            view.SetOffset(Vector2.zero);
        }

        IEnumerator Recoil(BattleSideView view, Vector2 back, double seconds)
        {
            yield return Move(view, Vector2.zero, back, (float)seconds * 0.6f);
            yield return Move(view, back, Vector2.zero, (float)seconds * 0.4f);
        }

        IEnumerator Dodge(BattleSideView view)
        {
            var step = new Vector2(view == sides[BattleState.Npc] ? 16 : -16, 0);
            yield return Move(view, Vector2.zero, step, 0.08f);
            yield return VfxLayer.Wait(0.12f);
            yield return Move(view, step, Vector2.zero, 0.12f);
        }

        static IEnumerator Move(BattleSideView view, Vector2 from, Vector2 to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                view.SetOffset(Vector2.Lerp(from, to, t / seconds));
                yield return null;
            }
            view.SetOffset(to);
        }

        /// <summary>두 코루틴을 동시에 돌리고 둘 다 끝날 때까지 기다린다</summary>
        IEnumerator Parallel(IEnumerator a, IEnumerator b)
        {
            bool doneA = false, doneB = false;
            host.StartCoroutine(Run(a, () => doneA = true));
            host.StartCoroutine(Run(b, () => doneB = true));
            while (!doneA || !doneB) yield return null;
        }

        static IEnumerator Run(IEnumerator routine, Action done)
        {
            yield return routine;
            done();
        }
    }
}
