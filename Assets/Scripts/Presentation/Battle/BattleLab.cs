using System;
using System.Collections;
using System.Linq;
using Pokepedia.App.Battle;
using Pokepedia.Content;
using Pokepedia.Domain.Battle;
using Pokepedia.Presentation.Battle.Fx;
using Pokepedia.Presentation.UI;
using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 연출 실험실 (주소에 ?lab, 또는 타이틀에서 L): 전용 연출·몸통박치기·VS 도입·승리 연출을 골라 반복 재생한다.
    /// 결과 버튼으로 명중/빗나감/급소를 돌린다. HP 는 바뀌지 않는다. 고른 결과는 animation.json 의 moveFx 에 적는다.
    /// 개발용이라 배틀 흐름(BattleScreen)과 떼어 둔다.
    /// </summary>
    public sealed class BattleLab
    {
        static readonly string[] Outcomes = { "Hit", "Miss", "Crit" };   // 명중 / 빗나감 / 급소, 고를 때마다 돌아간다

        readonly MonoBehaviour host;
        readonly BattleHud hud;
        readonly BattleContext ctx;
        readonly BattleEventPlayer player;
        readonly Action<ChoiceMenu> showMenu;   // 메뉴 하나만 켜고 입력을 받게 한다(BattleScreen.SetMenu)
        readonly Action<bool> setPlaying;       // 재생 중이면 true (BattleScreen 의 표시 상태를 Animating ↔ Lab 으로)
        readonly Action exit;
        ChoiceMenu menu;
        int index, outcome;

        public BattleLab(MonoBehaviour host, BattleHud hud, BattleContext ctx, BattleEventPlayer player,
                         Action<ChoiceMenu> showMenu, Action<bool> setPlaying, Action exit)
        {
            this.host = host; this.hud = hud; this.ctx = ctx; this.player = player;
            this.showMenu = showMenu; this.setPlaying = setPlaying; this.exit = exit;
        }

        public IEnumerator Intro()
        {
            hud.ResetStage();
            foreach (var e in ctx.Session.Start())
            {
                if (!(e is PokemonSentOut s)) continue;
                var view = hud.Sides[s.Side];
                view.Name.text = ctx.Names.Species(s.SpeciesId);
                view.Hp.Set(s.Hp, s.MaxHp);
                yield return hud.Director.SendOut(s.Side, ctx.Catalog.GetSpecies(s.SpeciesId));
                view.InfoBox.SetActive(true);
            }
            ShowMenu();
        }

        void ShowMenu()
        {
            var effects = AnimationDirector.LabEffects;
            if (menu == null)
            {
                int count = effects.Length + 3;   // 전용 연출 + VS + Victory + 결과 버튼
                var cells = new Vector3[count];
                for (int i = 0; i < count; i++) cells[i] = new Vector3(4 + (i % 3) * 76, 4 + (i / 3) * 17, 74);
                var inner = Ui.Box(hud.Screen, "LabMenu", 2, 156, 236, 58);
                menu = new ChoiceMenu(inner.parent.gameObject, inner, hud.Font, cells, 3);
                menu.Chosen += OnChosen;
                menu.Cancelled += Exit;
                hud.WireSounds(menu);
                hud.Vfx.BringToFront();
            }
            var labels = effects.Select(e => e.Label).Concat(new[] { "VS", "Victory", Outcomes[outcome] }).ToArray();
            setPlaying(false);
            showMenu(menu);
            menu.Show(labels, index);
        }

        void OnChosen(int i)
        {
            index = i;
            var effects = AnimationDirector.LabEffects;
            if (i == effects.Length + 2) { outcome = (outcome + 1) % Outcomes.Length; ShowMenu(); return; }
            menu.Hide();
            showMenu(null);
            setPlaying(true);
            host.StartCoroutine(Play(i));
        }

        IEnumerator Play(int i)
        {
            var effects = AnimationDirector.LabEffects;
            if (i == effects.Length)
            {
                yield return player.PlayVs();
            }
            else if (i == effects.Length + 1)
            {
                yield return hud.Director.Victory(BattleState.Player);
                yield return VfxLayer.Wait(3f);
                hud.World.Backdrop?.SetWeather(1, 1.2f);
                host.StartCoroutine(hud.Lighting.FadeAmbient(hud.Theme.Ambient, 1.2f));
            }
            else
            {
                var e = effects[i];
                int attacker = ctx.SpeciesOf(e.Side), target = ctx.SpeciesOf(BattleState.Opponent(e.Side));
                // 전용 연출은 대표 기술로, 몸통박치기(contact)는 기본기로 재생한다(색·소리가 그 기술의 타입을 따른다)
                var role = e.Id == "contact" ? MoveRole.Basic : MoveRole.Signature;
                var move = ctx.Catalog.MoveSetFor(attacker).First(m => m.Role == role);
                bool crit = outcome == 2;
                yield return hud.Director.PlayLab(e.Id, e.Side, attacker, target, move, outcome == 1, crit);
                if (crit) yield return hud.Message.Say(ctx.Locale.Get(BattleTextKeys.CriticalHit));
            }
            yield return VfxLayer.Wait(0.3f);
            ShowMenu();
        }

        void Exit()
        {
            menu.Hide();
            showMenu(null);
            exit();
        }
    }
}
