using System;
using System.Collections;
using System.Collections.Generic;
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
    /// 전용 연출(moveFx)은 후보를 여러 개 두고 연출 실험실(?lab)에서 골라 animation.json 에 적는다.
    /// </summary>
    public sealed class AnimationDirector
    {
        /// <summary>연출 실험실에서 고를 수 있는 전용 연출 (id, 설명). 실제 배정은 animation.json 의 moveFx.</summary>
        public static readonly (string Id, string Label, int Side)[] LabEffects =
        {
            ("thunder_arc", "TB-A arc", BattleState.Player),
            ("thunder_fork", "TB-B fork", BattleState.Player),
            ("lightning_strike", "TB-C sky", BattleState.Player),
            ("hydro_beam", "HP-A beam", BattleState.Npc),
            ("hydro_geyser", "WP geyser", BattleState.Npc),
            ("contact", "Tackle", BattleState.Player),
            ("contact", "Tackle(N)", BattleState.Npc),
            (TrainerBallLabId, "Ball", BattleState.Npc),
        };

        /// <summary>실험실 전용: 기술이 아니라 "트레이너가 처리한다"의 몬스터볼(TrainerBall)을 재생한다</summary>
        public const string TrainerBallLabId = "trainer_ball";

        readonly MonoBehaviour host;
        readonly BattleSideView[] sides;
        readonly VfxLayer vfx;
        readonly SoundBank sound;
        readonly AnimationCatalog anim;
        readonly ContentCatalog catalog;
        readonly BattleWorld world;
        readonly BattleLighting lighting;

        public AnimationDirector(MonoBehaviour host, BattleSideView[] sides, VfxLayer vfx, SoundBank sound,
                                 AnimationCatalog anim, ContentCatalog catalog, BattleWorld world, BattleLighting lighting)
        {
            this.host = host; this.sides = sides; this.vfx = vfx; this.sound = sound; this.anim = anim; this.catalog = catalog;
            this.world = world; this.lighting = lighting;
        }

        AnimationProfile ProfileOf(int speciesId) => anim.ProfileFor(catalog.GetSpecies(speciesId).AnimProfile);

        static Color Rgb(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        static bool IsWaterFx(string fx) => fx.StartsWith("hydro_", StringComparison.Ordinal);

        // ------------------------------------------------------------------ 등장 · 기절 · 승리
        /// <param name="shiny">색이 다른 스티커로 들어온 플레이어 포켓몬 (Collection, 외형만)</param>
        public IEnumerator SendOut(int side, SpeciesInfo info, bool shiny = false)
        {
            var view = sides[side];
            var sprites = info.Sprites;
            view.Play(SpriteSheet.Load(side == BattleState.Player ? (shiny ? sprites.BackShiny : sprites.Back) : (shiny ? sprites.FrontShiny : sprites.Front)));
            view.SetAlpha(1);
            view.SetFlash(0);
            // NPC 는 오른쪽에서, 플레이어는 왼쪽에서 미끄러져 들어온다. 등장음의 "뿅"은 번쩍이는 순간(BattleSfx.SendOutPopAt)에 맞춰져 있다.
            // 울음소리는 M3에서 넣지 않는다(PROJECT.md 8.3) — 속성 소리는 공격음이므로 여기서 쓰지 않는다.
            sound.Play(SfxKind.SendOut);
            var from = new Vector2(side == BattleState.Npc ? 150 : -150, 0);
            yield return Move(view, from, Vector2.zero, (float)BattleSfx.SendOutPopAt);
            view.SetFlash(0.8f); yield return VfxLayer.Wait(0.06f); view.SetFlash(0);
        }

        public IEnumerator Faint(int side)
        {
            var view = sides[side];
            sound.Play(SfxKind.Faint);
            for (float t = 0; t < 0.45f; t += Time.deltaTime)
            {
                float k = t / 0.45f;
                view.SetOffset(new Vector2(0, 40 * k * k));
                view.SetAlpha(1 - k);
                yield return null;
            }
            view.SetVisible(false);
            view.SetOffset(Vector2.zero);
            view.SetAlpha(1);
        }

        /// <summary>
        /// 승리: 비가 그치고 노을 진 맑은 하늘로 바뀐다(날씨·전역 조명이 1.4초에 걸쳐 바뀜). 이긴 쪽은 빛 번짐·반짝임 속에서
        /// 세 번 뛰고(그림자는 바닥에 남음), 카메라가 이긴 쪽으로 다가간다. 다음 판이 시작되면 BattleScreen 이 날씨를 되돌린다.
        /// </summary>
        public IEnumerator Victory(int side)
        {
            var view = sides[side];
            var gold = new Color(1f, 0.86f, 0.42f);
            if (world.Backdrop != null)
            {
                world.Backdrop.SetWeather(0, 1.4f);
                host.StartCoroutine(lighting.FadeAmbient(world.Backdrop.Theme.ClearAmbient, 1.4f));
            }
            if (!view.Visible) yield break;   // 트레이너가 처리한 승리: 쓰러진 쪽은 날씨만 갠다
            host.StartCoroutine(world.CameraTo(BattleWorld.Toward(view.Center, 0.3f), 1.1f, 0.7f));   // 이긴 쪽으로 다가가며 살짝 확대
            host.StartCoroutine(lighting.Pulse(view.Center, gold, 150, 1.9f, 1.3f));
            host.StartCoroutine(vfx.Rings(view.Center, gold, 2, 0.7f));
            vfx.Burst(view.Center, gold, Color.white, 46, 30, 110, 1.0f, 3, 150, 16);
            for (int i = 0; i < 3; i++)
            {
                float h = i == 2 ? 14 : 10;
                yield return Move(view, Vector2.zero, new Vector2(0, -h), 0.11f, true);
                vfx.Burst(view.Center + new Vector2(0, -h), gold, Color.white, 10, 15, 60, 0.6f, 2, 120, 8);
                yield return Move(view, new Vector2(0, -h), Vector2.zero, 0.13f, true);
            }
            yield return VfxLayer.Wait(0.4f);
            host.StartCoroutine(world.CameraTo(Vector2.zero, 1, 0.9f));
        }

        /// <summary>
        /// 응원 부활 (PROJECT.md 5.3): 카메라가 쓰러진 쪽으로 다가가고, 하늘에서 금빛 빛기둥이 내려오며 화면 전체에 성스러운 깃털이
        /// 흩날린다. 빛 속에서 포켓몬이 아래에서 떠오르며 다시 나타나고, 금빛으로 화면이 번쩍인 뒤 HP 가 다시 찬다(hpAnimation).
        /// </summary>
        public IEnumerator Revive(int side, Func<IEnumerator> hpAnimation)
        {
            var view = sides[side];
            var gold = new Color(1f, 0.86f, 0.42f);
            var holy = new Color(1f, 0.95f, 0.75f, 0.9f);
            sound.Play(SfxKind.Charge);
            host.StartCoroutine(world.CameraTo(BattleWorld.Toward(view.Center, 0.25f), 1.08f, 0.8f));
            host.StartCoroutine(vfx.LightShaft(view.Feet, 46, holy, 2.6f));
            host.StartCoroutine(vfx.Feathers(26, 3.2f, gold));
            host.StartCoroutine(lighting.Pulse(view.Center, gold, 170, 2.0f, 2.4f));
            yield return VfxLayer.Wait(0.55f);

            view.SetVisible(true);
            vfx.Burst(view.Feet, gold, Color.white, 40, 15, 70, 1.2f, 2, -50, 20);   // 발밑에서 피어오르는 반짝임
            const float rise = 0.7f, from = 26;
            for (float t = 0; t < rise; t += Time.deltaTime)
            {
                float k = t / rise, e = 1 - (1 - k) * (1 - k);
                view.SetOffset(new Vector2(0, Mathf.Round(from * (1 - e))));
                view.SetAlpha(e);
                view.SetFlash(1 - 0.6f * k);
                yield return null;
            }
            view.SetOffset(Vector2.zero);
            view.SetAlpha(1);
            sound.Play(SfxKind.SendOut);
            host.StartCoroutine(vfx.ScreenFlash(new Color(1f, 0.93f, 0.7f, 0.75f), 0.55f));
            host.StartCoroutine(vfx.Rings(view.Center, gold, 3, 0.8f));
            vfx.Burst(view.Center, gold, Color.white, 30, 30, 120, 0.8f, 2, 120, 12);
            view.SetFlash(0.9f); yield return VfxLayer.Wait(0.1f); view.SetFlash(0);
            if (hpAnimation != null) yield return hpAnimation();
            yield return VfxLayer.Wait(0.3f);
            host.StartCoroutine(world.CameraTo(Vector2.zero, 1, 0.6f));
        }

        /// <summary>
        /// 트레이너가 처리한다 (PROJECT.md 5.3): 화면 왼쪽 아래(트레이너 자리)에서 몬스터볼이 돌면서 포물선으로 날아가 상대 머리에 "퍽" 맞고
        /// 튕겨 나간다. 맞은 상대는 번쩍이며 움찔한 뒤 머리 위에 별이 돌고 비틀거린다. 쓰러지는 것은 뒤따르는 Fainted 가 그린다.
        /// </summary>
        public IEnumerator TrainerBall(int targetSide)
        {
            var target = sides[targetSide];
            var head = target.Center - new Vector2(0, Mathf.Round(target.Size.y * 0.3f));
            host.StartCoroutine(world.CameraTo(BattleWorld.Toward(target.Center, 0.3f), 1.08f, 0.5f));
            sound.Play(SfxKind.Miss);   // 휙
            yield return vfx.ThrowBall(new Vector2(-12, 150), head, 50, 0.6f);

            sound.Play(SfxKind.CriticalHit);
            var gold = new Color(1f, 0.85f, 0.4f);
            host.StartCoroutine(vfx.ImpactStar(head, Color.white, gold, 0.25f));
            vfx.Burst(head, Color.white, gold, 16, 40, 120, 0.35f, 2);
            host.StartCoroutine(vfx.Shake(3, 0.25f));
            host.StartCoroutine(world.Punch(0.06f, 0.25f));
            host.StartCoroutine(lighting.Pulse(head, Color.white, 80, 1.4f, 0.3f));
            host.StartCoroutine(vfx.BounceBall(head, new Vector2(-55, -130), 480, 1.0f));
            for (int i = 0; i < 2; i++)
            {
                target.SetFlash(1); target.SetOffset(new Vector2(0, 3));
                yield return VfxLayer.Wait(0.06f);
                target.SetFlash(0); target.SetOffset(Vector2.zero);
                yield return VfxLayer.Wait(0.06f);
            }
            host.StartCoroutine(vfx.Dizzy(head - new Vector2(0, 8), Mathf.Round(target.Size.x * 0.25f), 1.2f));
            for (float t = 0; t < 1.0f; t += Time.deltaTime)   // 비틀비틀
            {
                target.SetOffset(new Vector2(Mathf.Round(Mathf.Sin(t * 13) * 3), 0));
                yield return null;
            }
            target.SetOffset(Vector2.zero);
            host.StartCoroutine(world.CameraTo(Vector2.zero, 1, 0.4f));
        }

        // ------------------------------------------------------------------ 기술
        /// <param name="hit">명중했으면 피해 이벤트, 빗나갔으면 null</param>
        /// <param name="hpAnimation">HP 바를 줄이는 코루틴 (화면이 넘긴다)</param>
        public IEnumerator UseMove(int side, int attackerSpeciesId, int targetSpeciesId, MoveInfo move, DamageDealt hit, Func<IEnumerator> hpAnimation)
        {
            // 카메라는 UseMoveCore 안에서 공격자 → 상대로 움직이고(AttackCamera), 끝나면 가운데·1배로 돌아온다
            yield return UseMoveCore(side, attackerSpeciesId, targetSpeciesId, move, hit, hpAnimation, null);
            host.StartCoroutine(world.CameraTo(Vector2.zero, 1, 0.3f));
        }

        /// <summary>연출 실험실(?lab): 전용 연출 fx 를 강제로 지정해 재생한다. HP 는 바꾸지 않는다.</summary>
        public IEnumerator PlayLab(string fx, int side, int attackerSpeciesId, int targetSpeciesId, MoveInfo move, bool missed, bool critical)
        {
            var hit = missed ? null : new DamageDealt(BattleState.Opponent(side), 1, 0, 2.0, critical);
            yield return UseMoveCore(side, attackerSpeciesId, targetSpeciesId, move, hit, null, fx);
            host.StartCoroutine(world.PanTo(0, 0.25f));
        }

        IEnumerator UseMoveCore(int side, int attackerSpeciesId, int targetSpeciesId, MoveInfo move, DamageDealt hit,
                                Func<IEnumerator> hpAnimation, string fxOverride)
        {
            var attacker = sides[side];
            var target = sides[BattleState.Opponent(side)];
            var prof = ProfileOf(attackerSpeciesId);
            var targetProf = ProfileOf(targetSpeciesId);
            var tv = anim.TypeFor(move.Move.Type);
            Color c1 = Rgb(tv.PrimaryRgb), c2 = Rgb(tv.SecondaryRgb);
            var typeName = ContentLoader.ToJsonName(move.Move.Type);
            bool missed = hit == null;

            var fx = fxOverride ?? anim.MoveFxFor(prof.Key, move.Move.Id) ?? move.AnimKind switch
            {
                AnimKind.Contact => "contact",
                AnimKind.Projectile => "projectile",
                _ => "effect",
            };

            ScheduleMoveSounds(fx, typeName, prof.WindupSec);
            host.StartCoroutine(AttackCamera(attacker, target, (float)ReleaseSec(fx, prof.WindupSec)));

            Vector2 toTarget = (target.Feet - attacker.Feet).normalized;
            switch (fx)
            {
                // ---------------- 10만볼트 후보 (A·B 는 몸에서 발사, C 는 예전의 하늘에서 떨어지는 낙뢰)
                case "thunder_arc":
                {
                    yield return Charge(attacker, c1, c2, prof.WindupSec, 26);
                    var from = Edge(attacker, target.Center);
                    var to = missed ? target.Center + new Vector2(30, -8) : Surface(target, from);
                    host.StartCoroutine(vfx.ScreenFlash(new Color(1f, 1f, 0.75f, 0.45f), 0.16f));
                    host.StartCoroutine(lighting.Pulse(from, c1, 70, 1.8f, 0.4f));
                    host.StartCoroutine(lighting.Pulse(to, c1, 130, 2.4f, 0.55f));
                    if (missed)
                    {
                        yield return Parallel(vfx.Lightning(from, to, Color.white, c1, 3, 0.36f, 10, 2), Dodge(target));
                        vfx.Burst(to, c1, c2, 14, 20, 60, 0.35f, 2);
                    }
                    else
                    {
                        yield return vfx.Lightning(from, to, Color.white, c1, 3, 0.42f, 10, 3);
                        vfx.Burst(target.Center, c1, c2, 40, 30, 110, 0.5f, 3, 0, 6);
                        host.StartCoroutine(vfx.Shake(2, 0.2f));
                    }
                    break;
                }

                case "thunder_fork":
                {
                    yield return Charge(attacker, c1, c2, prof.WindupSec, 26);
                    var from = Edge(attacker, target.Center);
                    var to = missed ? target.Center + new Vector2(30, -8) : Surface(target, from);
                    host.StartCoroutine(lighting.Pulse(from, c1, 80, 2.0f, 0.6f));
                    // 잔가지 세 줄기가 먼저 뻗고, 마지막에 굵은 줄기 하나가 꽂힌다
                    for (int k = 0; k < 3; k++)
                    {
                        var spread = new Vector2(0, (k - 1) * 18);
                        host.StartCoroutine(vfx.Lightning(from, to + spread, Color.white, c1, 2, 0.2f, 12, 1));
                        yield return VfxLayer.Wait(0.07f);
                    }
                    host.StartCoroutine(vfx.ScreenFlash(new Color(1f, 1f, 0.75f, 0.55f), 0.2f));
                    host.StartCoroutine(lighting.Pulse(to, c1, 140, 2.6f, 0.6f));
                    if (missed) yield return Parallel(vfx.Lightning(from, to, Color.white, c1, 3, 0.34f, 8, 3, 7), Dodge(target));
                    else
                    {
                        yield return vfx.Lightning(from, to, Color.white, c1, 3, 0.4f, 8, 4, 7);
                        vfx.Burst(target.Center, c1, c2, 50, 30, 120, 0.55f, 3, 0, 8);
                        host.StartCoroutine(vfx.Shake(3, 0.25f));
                    }
                    break;
                }

                case "lightning_strike":
                    yield return Charge(attacker, c1, c2, prof.WindupSec, 26);
                    host.StartCoroutine(vfx.ScreenFlash(new Color(1f, 1f, 0.7f, 0.55f), 0.18f));
                    host.StartCoroutine(lighting.Pulse(missed ? target.Center + new Vector2(28, 0) : target.Center, c1, 130, 2.4f, 0.5f));
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

                // ---------------- 하이드로펌프(A 물줄기)·물대포(C 3연발)
                case "hydro_beam":
                {
                    bool mouth = FromMouth(prof, side);
                    if (mouth) attacker.Animator.Hold(MouthFrame);
                    var muzzle = Muzzle(attacker, prof, side);
                    var impact = missed ? target.Center + new Vector2(40, -16) : Surface(target, muzzle);
                    var away = (impact - muzzle).normalized;
                    host.StartCoroutine(lighting.Pulse(muzzle, c1, 60, 1.2f, 0.4f));
                    if (!mouth) yield return Recoil(attacker, -toTarget * 4, prof.WindupSec * 0.7f);
                    else yield return VfxLayer.Wait((float)prof.WindupSec * 0.7f);                 // 입에서 쏠 때는 몸이 밀려 입과 물줄기가 벌어지지 않게
                    vfx.Spray(muzzle, away, 30, c2, Color.white, 14, 40, 110, 0.25f, 2, 0);   // 발사구 물보라
                    host.StartCoroutine(lighting.Pulse(muzzle, c1, 90, 1.6f, 0.85f));
                    host.StartCoroutine(Recoil(attacker, -toTarget * (mouth ? 1 : 8), 0.75f));
                    if (missed) yield return Parallel(vfx.WaterJet(muzzle, impact, c1, c2, 11, 0.12f, 0.55f, false), Dodge(target));
                    else
                    {
                        host.StartCoroutine(vfx.Shake(2, 0.7f));
                        host.StartCoroutine(lighting.Pulse(impact, c1, 110, 1.8f, 0.8f));
                        host.StartCoroutine(Shove(target, away * 10, 0.67f));
                        yield return vfx.WaterJet(muzzle, impact, c1, c2, 11, 0.12f, 0.55f);
                        HeavySplash(impact, -away, c1, c2, 1.1f);
                    }
                    attacker.Animator.Release();
                    break;
                }

                case "hydro_burst":
                {
                    bool mouth = FromMouth(prof, side);
                    if (mouth) attacker.Animator.Hold(MouthFrame);
                    var muzzle = Muzzle(attacker, prof, side);
                    var dest = missed ? target.Center + new Vector2(34, -14) : target.Center;
                    for (int k = 0; k < 3; k++)
                    {
                        bool last = k == 2;
                        yield return Recoil(attacker, -toTarget * (mouth ? 1 : last ? 7 : 4), last ? 0.16f : 0.1f);
                        host.StartCoroutine(lighting.Pulse(muzzle, c1, 60, 1.2f, 0.25f));
                        yield return vfx.Orb(muzzle, dest + new Vector2(0, (k - 1) * 5), c1, last ? 18 : 12, 0.18f, c2);
                        if (missed) { if (last) yield return Dodge(target); continue; }
                        vfx.Spray(dest, attacker.Center - dest, 70, c1, c2, last ? 50 : 22, 40, last ? 150 : 100, 0.5f, 3, 280);
                        host.StartCoroutine(vfx.Shake(last ? 4 : 2, last ? 0.3f : 0.12f));
                        target.SetFlash(1); yield return VfxLayer.Wait(0.04f); target.SetFlash(0);
                    }
                    if (!missed) HeavySplash(dest, attacker.Center - dest, c1, c2, 0.8f);
                    attacker.Animator.Release();
                    break;
                }

                // ---------------- 물의맹세: 상대 발밑이 울리며 물거품이 끓다가 땅에서 물기둥이 솟구쳐 상대를 띄운다
                case "hydro_geyser":
                {
                    var spot = missed ? target.Feet + new Vector2(32, 0) : target.Feet;   // 빗나가면 옆자리에서 솟는다
                    float prep = (float)ReleaseSec(fx, prof.WindupSec);
                    host.StartCoroutine(Recoil(attacker, new Vector2(0, 3), prep));     // 웅크렸다가
                    host.StartCoroutine(vfx.Shake(1, prep));
                    host.StartCoroutine(lighting.Pulse(spot, c1, 50, 0.9f, prep));
                    for (float t = 0, next = 0; t < prep; t += Time.deltaTime)
                    {
                        if (t >= next)
                        {
                            vfx.Spray(spot + new Vector2(UnityEngine.Random.Range(-14f, 14f), 0), new Vector2(0, -1), 25, c2, Color.white, 2, 25, 65, 0.3f, 2, 220);
                            next += 0.04f;
                        }
                        yield return null;
                    }
                    var top = spot - new Vector2(0, Mathf.Round(target.Size.y + 34));
                    float width = Mathf.Clamp(Mathf.Round(target.Size.x * 0.4f), 12, 22);
                    host.StartCoroutine(vfx.Shake(3, 0.6f));
                    host.StartCoroutine(lighting.Pulse(spot - new Vector2(0, Mathf.Round(target.Size.y / 2)), c1, 120, 1.9f, 0.8f));
                    vfx.Spray(spot, new Vector2(0, -1), 70, c1, Color.white, 40, 60, 150, 0.6f, 3, 300);   // 땅을 뚫고 튀는 물
                    vfx.Burst(spot + new Vector2(0, -2), new Color(0.55f, 0.5f, 0.4f, 0.9f), new Color(0.75f, 0.7f, 0.58f, 0.8f), 12, 10, 45, 0.45f, 2, 0, 10);   // 흙
                    const float lift = 10;
                    if (missed) host.StartCoroutine(Dodge(target));
                    else host.StartCoroutine(Move(target, Vector2.zero, new Vector2(0, -lift), 0.25f, true));   // 물기둥에 떠오른다(그림자는 바닥)
                    yield return vfx.WaterJet(spot + new Vector2(0, 4), top, c1, c2, width, 0.12f, 0.55f);
                    vfx.Spray(top, new Vector2(0, -1), 80, c1, c2, 40, 40, 120, 0.9f, 3, 320);   // 꼭대기에서 흩어져 떨어지는 물
                    if (!missed)
                    {
                        HeavySplash(target.Center, new Vector2(0, -1), c1, c2, 0.7f);
                        yield return Move(target, new Vector2(0, -lift), Vector2.zero, 0.12f, true);
                    }
                    break;
                }

                // ---------------- 공통 타임라인
                // ---------------- 몸통박치기(접촉): 도움닫기 → 잔상과 함께 돌진 → 몸 표면 충돌(별·파편·고리·흙먼지) → 복귀
                case "contact":
                {
                    float dist = (target.Center - attacker.Center).magnitude;
                    var reach = toTarget * Mathf.Round(Mathf.Max(Mathf.Max(prof.LungePx, 1) * 3, dist * 0.3f));
                    var back = -toTarget * 6;
                    // 도움닫기(준비 시간의 절반 = ReleaseSec): 살짝 물러났다가
                    yield return Move(attacker, Vector2.zero, back, Mathf.Max(0.08f, (float)prof.WindupSec * 0.5f));
                    host.StartCoroutine(Afterimages(attacker, 0.12f));
                    if (missed)
                    {
                        yield return Parallel(Move(attacker, back, reach, 0.11f), Dodge(target));
                        yield return Move(attacker, reach, Vector2.zero, 0.22f);
                    }
                    else
                    {
                        yield return Move(attacker, back, reach, 0.09f);
                        var hitAt = Surface(target, attacker.Center + reach);
                        host.StartCoroutine(vfx.ImpactStar(hitAt, Color.white, Color.Lerp(c1, new Color(1f, 0.85f, 0.4f), 0.5f), 0.22f));
                        vfx.Burst(hitAt, Color.white, c1, 14, 40, 120, 0.3f, 2);
                        host.StartCoroutine(vfx.Rings(hitAt, Color.Lerp(c1, Color.white, 0.6f), 1, 0.22f));
                        vfx.Burst(target.Feet + new Vector2(0, -2), new Color(0.55f, 0.5f, 0.4f, 0.9f), new Color(0.75f, 0.7f, 0.58f, 0.8f), 10, 8, 35, 0.4f, 2, 0, 8);   // 흙먼지
                        host.StartCoroutine(lighting.Pulse(hitAt, Color.Lerp(c1, Color.white, 0.5f), 75, 1.3f, 0.3f));
                        yield return VfxLayer.Wait(0.05f);   // 부딪힌 채로 아주 잠깐 멈춘다(무게감)
                        yield return Move(attacker, reach, Vector2.zero, 0.2f);
                    }
                    break;
                }

                case "projectile":
                    yield return Charge(attacker, c1, c2, prof.WindupSec, 10);
                    var dest2 = missed ? target.Center + new Vector2(36, 0) : target.Center;
                    if (missed) yield return Parallel(vfx.Orb(attacker.Center, dest2, c1, 10, 0.35f, c2), Dodge(target));
                    else
                    {
                        yield return vfx.Orb(attacker.Center, dest2, c1, 10, 0.35f, c2);
                        vfx.Burst(dest2, c1, c2, 24, 30, 90, 0.4f, 3);
                        host.StartCoroutine(lighting.Pulse(dest2, c1, 85, 1.5f, 0.4f));
                    }
                    break;

                default: // effect
                    yield return Charge(attacker, c1, c2, prof.WindupSec, 10);
                    if (missed) yield return Dodge(target);
                    else
                    {
                        host.StartCoroutine(lighting.Pulse(target.Center, c1, 95, 1.4f, 0.6f));
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

        /// <summary>
        /// 물 기술의 발사 위치. 앞모습(상대 쪽)이고 프로필에 입 위치(animation.json 의 mouth)가 있으면 입 바로 앞,
        /// 아니면 몸 위쪽(뒷모습은 입이 안 보인다).
        /// 스프라이트 크기에 비례하므로 다른 종에도 그럴듯하게 맞는다.
        /// </summary>
        static Vector2 Muzzle(BattleSideView v, AnimationProfile prof, int side)
        {
            if (FromMouth(prof, side))
                return v.Center + new Vector2(Mathf.Round(v.Size.x * (float)prof.Mouth.X), Mathf.Round(v.Size.y * (float)prof.Mouth.Y));
            return v.Center + new Vector2(0, -Mathf.Round(v.Size.y * 0.22f));
        }

        /// <summary>animation.json 의 mouth 는 앞모습 첫 프레임 기준이다. 입에서 쏘는 동안 이 프레임에 고정해야 입과 발사 위치가 맞는다(머리가 프레임마다 크게 움직인다).</summary>
        const int MouthFrame = 0;

        static bool FromMouth(AnimationProfile prof, int side) => prof?.Mouth != null && side == BattleState.Npc;

        /// <summary>
        /// 상대 몸의 표면 쪽 점: 가운데에서 공격이 오는 방향으로 몸 폭의 3할만큼 나온 곳. 물줄기·빔의 끝을 여기에 두면 몸을 뚫지 않고 부딪힌다.
        /// </summary>
        static Vector2 Surface(BattleSideView target, Vector2 from)
        {
            var toward = (from - target.Center).normalized;
            return target.Center + toward * Mathf.Round(Mathf.Min(target.Size.x, target.Size.y) * 0.3f);
        }

        /// <summary>공격자 몸 가장자리 바로 바깥(상대 쪽). 몸에서 뻗어 나가는 번개의 시작점 — 몸 안에서 시작하면 스프라이트와 겹쳐 보인다.</summary>
        static Vector2 Edge(BattleSideView attacker, Vector2 toward)
        {
            var dir = (toward - attacker.Center).normalized;
            return attacker.Center + dir * Mathf.Round(Mathf.Min(attacker.Size.x, attacker.Size.y) * 0.42f + 3);
        }

        /// <summary>맞는 동안 서서히 밀려난다 (물줄기 압력). 끝나면 HitReaction 이 제자리로 돌린다.</summary>
        static IEnumerator Shove(BattleSideView view, Vector2 by, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                view.SetOffset(by * Mathf.Sqrt(t / seconds));
                yield return null;
            }
            view.SetOffset(by);
        }

        /// <summary>물 기술 명중: 맞은 곳에서 되튀는 큰 물보라(back = 되튀는 방향) + 사방 물방울 + 흰 거품 + 빛 + 흔들림</summary>
        void HeavySplash(Vector2 at, Vector2 back, Color c1, Color c2, float scale)
        {
            vfx.Spray(at, back + new Vector2(0, -back.magnitude * 0.6f), 80, c1, c2, Mathf.RoundToInt(60 * scale), 60, 170 * scale, 0.7f, 3, 300);
            vfx.Burst(at, c1, c2, Mathf.RoundToInt(30 * scale), 30, 110 * scale, 0.6f, 3, 280, 8);
            vfx.Burst(at, Color.white, c2, Mathf.RoundToInt(24 * scale), 20, 80, 0.5f, 2, 120, 5);
            host.StartCoroutine(vfx.Rings(at, c2, 2, 0.4f));
            host.StartCoroutine(vfx.Shake(Mathf.RoundToInt(3 * scale), 0.3f));
            host.StartCoroutine(lighting.Pulse(at, c1, 110, 1.9f * scale, 0.55f));
        }

        /// <summary>기 모으기 소리를 넘길 만큼 긴 준비 동작(초)</summary>
        const double ChargeSoundMinSec = 0.25;

        /// <summary>
        /// 소리를 연출 타이밍에 맞춘다(PROJECT.md 8.3 "타이밍 동기화가 핵심").
        /// 발사 순간 = 준비 동작이 끝나는 시각. 공격음은 그보다 MoveLead 만큼 먼저 틀어 "터지는 순간"을 발사에 맞추고,
        /// 준비가 길면 그 앞을 기 모으기 소리로 채운 뒤 공격음이 시작될 때 끊는다. 물 기술은 반동이 준비 동작이라 기 모으기 소리가 없다.
        /// </summary>
        /// <summary>발사 순간 = 준비 동작이 끝나는 시각(돌진은 절반, 물 기술은 반동 7할). 소리와 카메라가 같이 쓴다.</summary>
        static double ReleaseSec(string fx, double windupSec)
            => fx == "contact" ? windupSec * 0.5 : fx == "hydro_geyser" ? windupSec * 0.7 + GeyserRumbleSec : IsWaterFx(fx) ? windupSec * 0.7 : windupSec;

        /// <summary>물의맹세: 물기둥이 솟기 전 땅이 울리고 거품이 끓는 시간</summary>
        const double GeyserRumbleSec = 0.4;

        /// <summary>
        /// 공격 카메라 (M5, 2.5D): 준비 동작 동안 공격자 쪽으로 다가가며 확대하고, 발사 순간 상대 쪽으로 빠르게 넘어간다.
        /// 맞는 순간의 당김(Punch)은 HitReaction 이 따로 건다. 끝나면 UseMove 가 가운데·1배로 되돌린다.
        /// </summary>
        IEnumerator AttackCamera(BattleSideView attacker, BattleSideView target, float release)
        {
            host.StartCoroutine(world.CameraTo(BattleWorld.Toward(attacker.Center, 0.35f), 1.12f, Mathf.Clamp(release, 0.15f, 0.4f)));
            yield return VfxLayer.Wait(release);
            host.StartCoroutine(world.CameraTo(BattleWorld.Toward(target.Center, 0.3f), 1.08f, 0.16f));
        }

        void ScheduleMoveSounds(string fx, string typeName, double windupSec)
        {
            bool water = IsWaterFx(fx);
            double release = ReleaseSec(fx, windupSec);
            bool charge = fx != "contact" && !water && windupSec >= ChargeSoundMinSec;
            bool hasMove = SoundBank.HasMoveSound(typeName);
            float cue = Mathf.Max(0, (float)release - (hasMove ? SoundBank.MoveLead(typeName) : 0));

            if (charge) sound.PlayStoppable(SfxKind.Charge);
            if (!charge && !hasMove) return;
            host.StartCoroutine(After(cue, () =>
            {
                if (charge) sound.StopStoppable();
                if (hasMove) sound.Move(typeName);
            }));
        }

        static IEnumerator After(float seconds, Action action)
        {
            if (seconds > 0) yield return new WaitForSeconds(seconds);
            action();
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
            host.StartCoroutine(world.Punch(hit.Critical ? 0.08f : 0.04f, hit.Critical ? 0.3f : 0.2f));
            if (hit.Critical)
            {
                host.StartCoroutine(vfx.ScreenFlash(new Color(1, 1, 1, 0.9f), 0.25f));
                host.StartCoroutine(vfx.Shake(4, 0.3f));
                host.StartCoroutine(lighting.Pulse(target.Center, Color.white, 110, 1.8f, 0.3f));
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
            if (total >= 0.2f) host.StartCoroutine(lighting.Pulse(view.Center, c1, 52, 0.9f, total));
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

        static IEnumerator Move(BattleSideView view, Vector2 from, Vector2 to, float seconds, bool airborne = false)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                view.SetOffset(Vector2.Lerp(from, to, t / seconds), airborne);
                yield return null;
            }
            view.SetOffset(to, airborne);
        }

        /// <summary>돌진하는 동안 지나간 자리에 잔상을 남긴다(3장, 각자 옅어지며 사라짐)</summary>
        IEnumerator Afterimages(BattleSideView view, float seconds)
        {
            var ghosts = new List<(SpriteRenderer sr, float born)>();
            float t = 0, next = 0;
            while (t < seconds + 0.2f)
            {
                if (t >= next && t < seconds) { ghosts.Add((view.Snapshot(new Color(1, 1, 1, 0.5f)), t)); next += seconds / 3; }
                foreach (var (sr, born) in ghosts)
                    if (sr != null) sr.color = new Color(1, 1, 1, Mathf.Max(0, 0.5f * (1 - (t - born) / 0.2f)));
                t += Time.deltaTime;
                yield return null;
            }
            foreach (var (sr, _) in ghosts) if (sr != null) UnityEngine.Object.Destroy(sr.gameObject);
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
