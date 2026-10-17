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
            ("hydro_twin", "HP-B twin", BattleState.Npc),
            ("hydro_burst", "HP-C 3x", BattleState.Npc),
            ("water_cannon", "HP-D old", BattleState.Npc),
        };

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

        static bool IsWaterFx(string fx) => fx == "water_cannon" || fx.StartsWith("hydro_", StringComparison.Ordinal);

        // ------------------------------------------------------------------ 등장 · 기절 · 승리
        public IEnumerator SendOut(int side, SpeciesInfo info)
        {
            var view = sides[side];
            view.Play(SpriteSheet.Load(side == BattleState.Player ? info.Sprites.Back : info.Sprites.Front));
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
            host.StartCoroutine(world.PanTo(side == BattleState.Player ? -8 : 8, 0.7f));
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
            host.StartCoroutine(world.PanTo(0, 0.9f));
        }

        // ------------------------------------------------------------------ 기술
        /// <param name="hit">명중했으면 피해 이벤트, 빗나갔으면 null</param>
        /// <param name="hpAnimation">HP 바를 줄이는 코루틴 (화면이 넘긴다)</param>
        public IEnumerator UseMove(int side, int attackerSpeciesId, int targetSpeciesId, MoveInfo move, DamageDealt hit, Func<IEnumerator> hpAnimation)
        {
            // 카메라가 공격하는 쪽으로 살짝 움직였다가 돌아온다 (배경 레이어가 다르게 따라가 깊이가 보인다)
            host.StartCoroutine(world.PanTo(side == BattleState.Player ? 5 : -5, 0.12f));
            yield return UseMoveCore(side, attackerSpeciesId, targetSpeciesId, move, hit, hpAnimation, null);
            host.StartCoroutine(world.PanTo(0, 0.25f));
        }

        /// <summary>연출 실험실(?lab): 전용 연출 fx 를 강제로 지정해 재생한다. HP 는 바꾸지 않는다.</summary>
        public IEnumerator PlayLab(string fx, int side, int attackerSpeciesId, int targetSpeciesId, MoveInfo move, bool missed)
        {
            var hit = missed ? null : new DamageDealt(BattleState.Opponent(side), 1, 0, 2.0, false);
            host.StartCoroutine(world.PanTo(side == BattleState.Player ? 5 : -5, 0.12f));
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

                // ---------------- 하이드로펌프 후보 (A 굵은 빔, B 쌍포, C 3연발, D 예전 물줄기)
                case "hydro_beam":
                {
                    var muzzle = Muzzle(attacker, prof, side, 0);
                    var impact = missed ? target.Center + new Vector2(40, -16) : Surface(target, muzzle);
                    var away = (impact - muzzle).normalized;
                    host.StartCoroutine(lighting.Pulse(muzzle, c1, 60, 1.2f, 0.4f));
                    yield return Recoil(attacker, -toTarget * 4, prof.WindupSec * 0.7f);
                    vfx.Spray(muzzle, away, 30, c2, Color.white, 14, 40, 110, 0.25f, 2, 0);          // 발사구 물보라
                    host.StartCoroutine(lighting.Pulse(muzzle, c1, 90, 1.6f, 0.85f));
                    host.StartCoroutine(Recoil(attacker, -toTarget * 8, 0.75f));
                    if (missed) yield return Parallel(vfx.WaterJet(muzzle, impact, c1, c2, 17, 0.12f, 0.55f, false), Dodge(target));
                    else
                    {
                        host.StartCoroutine(vfx.Shake(2, 0.7f));
                        host.StartCoroutine(lighting.Pulse(impact, c1, 110, 1.8f, 0.8f));
                        host.StartCoroutine(Shove(target, away * 10, 0.67f));
                        yield return vfx.WaterJet(muzzle, impact, c1, c2, 17, 0.12f, 0.55f);
                        HeavySplash(impact, -away, c1, c2, 1.1f);
                    }
                    break;
                }

                case "hydro_twin":
                {
                    var left = Muzzle(attacker, null, side, -1);
                    var right = Muzzle(attacker, null, side, 1);
                    var mid = Muzzle(attacker, null, side, 0);
                    var impact = missed ? target.Center + new Vector2(40, -16) : Surface(target, mid);
                    var away = (impact - mid).normalized;
                    yield return Recoil(attacker, -toTarget * 4, prof.WindupSec * 0.7f);
                    host.StartCoroutine(lighting.Pulse(left, c1, 70, 1.4f, 0.8f));
                    host.StartCoroutine(lighting.Pulse(right, c1, 70, 1.4f, 0.8f));
                    host.StartCoroutine(Recoil(attacker, -toTarget * 7, 0.7f));
                    host.StartCoroutine(vfx.WaterJet(left, impact, c1, c2, 11, 0.1f, 0.5f, !missed));
                    if (missed) yield return Parallel(vfx.WaterJet(right, impact, c1, c2, 11, 0.1f, 0.5f, false), Dodge(target));
                    else
                    {
                        host.StartCoroutine(vfx.Shake(3, 0.6f));
                        host.StartCoroutine(Shove(target, away * 12, 0.6f));
                        yield return vfx.WaterJet(right, impact, c1, c2, 11, 0.1f, 0.5f);
                        HeavySplash(impact, -away, c1, c2, 1.3f);
                    }
                    break;
                }

                case "hydro_burst":
                {
                    var muzzle = Muzzle(attacker, prof, side, 0);
                    var dest = missed ? target.Center + new Vector2(34, -14) : target.Center;
                    for (int k = 0; k < 3; k++)
                    {
                        bool last = k == 2;
                        yield return Recoil(attacker, -toTarget * (last ? 7 : 4), last ? 0.16f : 0.1f);
                        host.StartCoroutine(lighting.Pulse(muzzle, c1, 60, 1.2f, 0.25f));
                        yield return vfx.Orb(muzzle, dest + new Vector2(0, (k - 1) * 5), c1, last ? 18 : 12, 0.18f, c2);
                        if (missed) { if (last) yield return Dodge(target); continue; }
                        vfx.Spray(dest, attacker.Center - dest, 70, c1, c2, last ? 50 : 22, 40, last ? 150 : 100, 0.5f, 3, 280);
                        host.StartCoroutine(vfx.Shake(last ? 4 : 2, last ? 0.3f : 0.12f));
                        target.SetFlash(1); yield return VfxLayer.Wait(0.04f); target.SetFlash(0);
                    }
                    if (!missed) HeavySplash(dest, attacker.Center - dest, c1, c2, 0.8f);
                    break;
                }

                case "water_cannon":
                    host.StartCoroutine(lighting.Pulse(attacker.Center, c1, 60, 1.1f, 0.55f));
                    yield return Recoil(attacker, -toTarget * 5, prof.WindupSec);
                    yield return vfx.Stream(attacker.Center, missed ? target.Center + new Vector2(30, -10) : target.Center,
                                            c1, c2, 0.5f, 6, 260, 4, 3);
                    if (missed) yield return Dodge(target);
                    else
                    {
                        vfx.Burst(target.Center, c1, c2, 36, 40, 120, 0.55f, 3, 260, 4);
                        host.StartCoroutine(lighting.Pulse(target.Center, c1, 90, 1.6f, 0.55f));
                    }
                    break;

                // ---------------- 공통 타임라인
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
                        host.StartCoroutine(lighting.Pulse(target.Center, Color.Lerp(c1, Color.white, 0.5f), 75, 1.3f, 0.3f));
                        yield return Move(attacker, reach, Vector2.zero, 0.15f);
                    }
                    break;

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
        /// 아니면 몸 위쪽(뒷모습은 입이 안 보인다). lane 은 쌍포용 좌우(-1, 0, 1, 쌍포는 등껍질 대포라 입을 쓰지 않는다).
        /// 스프라이트 크기에 비례하므로 다른 종에도 그럴듯하게 맞는다.
        /// </summary>
        static Vector2 Muzzle(BattleSideView v, AnimationProfile prof, int side, int lane)
        {
            if (prof?.Mouth != null && side == BattleState.Npc)
                return v.Center + new Vector2(Mathf.Round(v.Size.x * (float)prof.Mouth.X) - 2, Mathf.Round(v.Size.y * (float)prof.Mouth.Y));
            return v.Center + new Vector2(Mathf.Round(v.Size.x * 0.2f * lane), -Mathf.Round(v.Size.y * 0.22f));
        }

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
        void ScheduleMoveSounds(string fx, string typeName, double windupSec)
        {
            bool water = IsWaterFx(fx);
            double release = fx == "contact" ? windupSec * 0.5 : water && fx != "water_cannon" ? windupSec * 0.7 : windupSec;
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
