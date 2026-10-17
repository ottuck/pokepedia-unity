using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pokepedia.App.Battle;
using Pokepedia.Content;
using Pokepedia.Domain.Battle;
using Pokepedia.Presentation.Audio.Synth;
using Pokepedia.Presentation.Battle.Fx;
using Pokepedia.Presentation.UI;
using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>이번 판의 데이터: 콘텐츠·연출 카탈로그, 로케일·이름, 세션. BattleScreen 이 판마다 채우고 나머지가 읽는다.</summary>
    public sealed class BattleContext
    {
        public ContentCatalog Catalog;
        public AnimationCatalog Animation;
        public LocaleTable Locale;
        public BattleNames Names;
        public BattleSession Session;
        /// <summary>Collection 모드에서 플레이어가 색이 다른 스티커로 들어왔다 (외형만, PROJECT.md 9.2)</summary>
        public bool PlayerShiny;

        public int SpeciesOf(int side) => Session.State.Sides[side].Active.Species.Id;
        public bool ShinyOf(int side) => side == BattleState.Player && PlayerShiny;
    }

    /// <summary>
    /// 엔진이 돌려준 BattleEvent 를 순서대로 "재생"한다 (PROJECT.md 7.3): 메시지는 BattleMessages, 움직임·이펙트·소리는 AnimationDirector.
    /// 규칙은 판단하지 않는다. 도입 VS 장면과 승리 연출 핸들도 여기서 다룬다.
    /// </summary>
    public sealed class BattleEventPlayer
    {
        readonly MonoBehaviour host;
        readonly BattleHud hud;
        readonly BattleContext ctx;
        Coroutine victory;   // 진행 중인 승리 연출 (새 판이 시작되면 멈춘다)

        public BattleEventPlayer(MonoBehaviour host, BattleHud hud, BattleContext ctx)
        {
            this.host = host; this.hud = hud; this.ctx = ctx;
        }

        /// <summary>승리 메시지를 빨리 넘기면 승리 연출(뛰기·카메라)이 아직 돌고 있다. 새 판의 포켓몬을 움직이지 않게 멈춘다.</summary>
        public void StopVictory()
        {
            if (victory != null) { host.StopCoroutine(victory); victory = null; }
        }

        /// <summary>도입 VS 장면 (M5): 두 포켓몬의 앞모습·이름·첫 타입 색은 데이터에서 가져온다.</summary>
        public IEnumerator PlayVs()
        {
            yield return hud.Vs.Play(VsSide(BattleState.Player), VsSide(BattleState.Npc));
        }

        VsIntro.Side VsSide(int side)
        {
            int speciesId = ctx.SpeciesOf(side);
            var info = ctx.Catalog.GetSpecies(speciesId);
            var tv = ctx.Animation.TypeFor(info.Species.Types[0]);
            var front = ctx.ShinyOf(side) ? info.Sprites.FrontShiny : info.Sprites.Front;
            return new VsIntro.Side(SpriteSheet.Load(front), ctx.Names.Species(speciesId), Ui.Hex(tv.PrimaryRgb), Ui.Hex(tv.SecondaryRgb));
        }

        public IEnumerator Play(IReadOnlyList<BattleEvent> events)
        {
            var sides = hud.Sides;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e)
                {
                    case PokemonSentOut s:
                    {
                        var view = sides[s.Side];
                        view.Name.text = ctx.Names.Species(s.SpeciesId);
                        view.Hp.Set(s.Hp, s.MaxHp);
                        view.InfoBox.SetActive(false);
                        yield return hud.Director.SendOut(s.Side, ctx.Catalog.GetSpecies(s.SpeciesId), ctx.ShinyOf(s.Side));
                        view.InfoBox.SetActive(true);
                        yield return SayAll(e);
                        break;
                    }
                    case MoveUsed m:
                    {
                        yield return SayAll(e);
                        // 다음 이벤트(명중 → DamageDealt / 빗나감 → MoveMissed)를 미리 보고 한 동작으로 연출한다
                        var next = i + 1 < events.Count ? events[i + 1] : null;
                        var hit = next as DamageDealt;
                        Func<IEnumerator> hpAnim = hit == null ? null : (Func<IEnumerator>)(() => sides[hit.TargetSide].Hp.AnimateTo(hit.HpAfter));
                        yield return hud.Director.UseMove(m.Side, ctx.SpeciesOf(m.Side), ctx.SpeciesOf(BattleState.Opponent(m.Side)),
                                                          ctx.Catalog.GetMove(m.MoveId), hit, hpAnim);
                        if (hit != null || next is MoveMissed)
                        {
                            i++;
                            yield return SayAll(next); // "급소에 맞았다!" / "효과가 굉장했다!" / "공격은 빗나갔다!"
                        }
                        break;
                    }
                    case Fainted f:
                        yield return hud.Director.Faint(f.Side);
                        yield return SayAll(e);
                        break;
                    case Revived r:
                    {
                        // "트레이너의 응원이 ○○에게 닿았다!" → 다시 일어서는 연출·HP 회복 → "○○은(는) 다시 일어섰다!"
                        var lines = BattleMessages.For(e, ctx.Names, ctx.SpeciesOf);
                        yield return hud.Message.Say(ctx.Locale.Format(lines[0].Key, lines[0].Args.Cast<object>().ToArray()));
                        yield return hud.Director.Revive(r.Side, () => sides[r.Side].Hp.AnimateTo(r.Hp));
                        yield return hud.Message.Say(ctx.Locale.Format(lines[1].Key, lines[1].Args.Cast<object>().ToArray()));
                        break;
                    }
                    case BattleEnded b:
                        yield return hud.Sound.FadeOutMusic(0.25f);
                        hud.Sound.Play(b.WinnerSide == BattleState.Player ? SfxKind.Victory : SfxKind.Defeat);
                        if (b.WinnerSide == BattleState.Player) victory = host.StartCoroutine(hud.Director.Victory(b.WinnerSide));
                        yield return SayAll(e);
                        break;
                    default:
                        yield return SayAll(e);
                        break;
                }
            }
        }

        public IEnumerator SayAll(BattleEvent e)
        {
            foreach (var line in BattleMessages.For(e, ctx.Names, ctx.SpeciesOf))
                yield return hud.Message.Say(ctx.Locale.Format(line.Key, line.Args.Cast<object>().ToArray()));
        }
    }
}
