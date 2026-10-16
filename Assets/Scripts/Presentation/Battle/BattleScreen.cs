using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pokepedia.App.Battle;
using Pokepedia.Content;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;
using Pokepedia.Presentation.Audio;
using Pokepedia.Presentation.Audio.Synth;
using Pokepedia.Presentation.Battle.Fx;
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 배틀 화면 (PROJECT.md 5.3): 게임보이식 레이아웃, 행동 메뉴(싸우다/도망) → 기술 2×2 → 턴 재생 → 결과 메뉴.
    /// 규칙은 판단하지 않는다. BattleSession 이 돌려준 BattleEvent 를 순서대로 "재생"만 하고,
    /// 움직임·이펙트·소리는 AnimationDirector 에 맡긴다 (PROJECT.md 7.3).
    /// </summary>
    public sealed class BattleScreen : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset font;
        [SerializeField] TextAsset speciesJson;
        [SerializeField] TextAsset movesJson;
        [SerializeField] TextAsset rosterJson;
        [SerializeField] TextAsset animationJson;
        [SerializeField] TextAsset localeKo;
        [SerializeField] TextAsset localeEn;
        [SerializeField] TextAsset localeJa;
        [SerializeField] Material flashMaterial;     // Pokepedia/UI Flash (피격 번쩍임)
        [SerializeField] Material particleMaterial;  // 파티클 (URP 2D Sprite-Unlit)

        const int DemoPlayer = 25; // 피카츄
        const int DemoNpc = 9;     // 거북왕
        const int CanvasOrder = 10;

        /// <summary>화면 문구 키. 단일 출처는 Application 의 BattleTextKeys (로케일 파일 검사도 그쪽 테스트가 한다).</summary>
        static class Keys
        {
            public const string Fight = BattleTextKeys.MenuFight;
            public const string Run = BattleTextKeys.MenuRun;
            public const string WhatToDo = BattleTextKeys.PromptWhatToDo;
            public const string NextOpponent = BattleTextKeys.ResultNextOpponent;
            public const string Retry = BattleTextKeys.ResultRetry;
            public const string ToTitle = BattleTextKeys.ResultTitle;
            public const string InfoPower = BattleTextKeys.InfoPower;
            public const string InfoAccuracy = BattleTextKeys.InfoAccuracy;
            public static string Type(PokemonType t) => BattleTextKeys.TypeName(t);
        }

        ContentCatalog catalog;
        AnimationCatalog animation;
        LocaleTable locale;
        BattleNames names;
        BattleSession session;
        Action onExit;

        GameObject root;
        readonly BattleSideView[] sides = new BattleSideView[2];
        VfxLayer vfx;
        AnimationDirector director;
        SoundBank sound;
        MessageBox message;
        ChoiceMenu actionMenu, moveMenu, resultMenu;
        TextMeshProUGUI moveInfo;
        GameObject moveInfoBox;
        ChoiceMenu activeMenu;
        bool lastWin;
        int lastMoveIndex;

        /// <summary>타이틀에서 호출. Demo 매치업(피카츄 vs 거북왕)을 시작한다. onExit 는 [처음으로].</summary>
        public void BeginDemo(string localeCode, Action onExit)
        {
            this.onExit = onExit;
            if (catalog == null)
            {
                catalog = ContentLoader.Load(speciesJson.text, movesJson.text, rosterJson.text);
                animation = AnimationCatalog.Parse(animationJson.text);
            }
            var asset = localeCode switch { "en" => localeEn, "ja" => localeJa, _ => localeKo };
            locale = LocaleTable.Parse(asset.text);
            names = new BattleNames(id => locale.Get(catalog.GetSpecies(id).NameKey), id => locale.Get(catalog.GetMove(id).NameKey));
            if (root == null) Build();
            root.SetActive(true);

            var seed = (uint)Environment.TickCount | 1u;
            session = new BattleSession(catalog, BattleRules.Default, new SeededRandom(seed), BattleMode.Demo, DemoPlayer, () => DemoNpc);
            lastMoveIndex = 0;
            StartCoroutine(NewBattle(session.Start));
        }

        // ------------------------------------------------------------------ 레이아웃
        void Build()
        {
            var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PixelCanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            // 카메라 공간 캔버스: 파티클(월드 렌더러)이 UI 와 같은 평면에 그려지게 한다
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.planeDistance = 1;
            canvas.sortingOrder = CanvasOrder;
            root = canvasGo;
            sound = new SoundBank(gameObject);

            // 240×216 화면을 캔버스 가운데에 둔다 (정수 배율은 PixelCanvasScaler)
            var screen = new GameObject("Screen", typeof(RectTransform), typeof(Image));
            screen.transform.SetParent(canvasGo.transform, false);
            var srt = (RectTransform)screen.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(PixelCanvasScaler.BaseWidth, PixelCanvasScaler.BaseHeight);
            screen.GetComponent<Image>().color = Ui.Background;

            // 발판 → 포켓몬 → 이펙트 → 정보 상자·메시지 순으로 그린다 (나중에 만든 것이 위)
            Ui.Ellipse(srt, "NpcPlatform", 178, 82, 104, 18, Ui.Platform);
            Ui.Ellipse(srt, "PlayerPlatform", 66, 150, 108, 18, Ui.Platform);
            var npcSprite = MakeSprite(srt, "NpcSprite");
            var playerSprite = MakeSprite(srt, "PlayerSprite");
            vfx = new VfxLayer(srt, particleMaterial, CanvasOrder);
            sides[BattleState.Npc] = MakeSide(srt, "Npc", npcSprite, new Vector2(178, 86), 6, 6, 116, 32, false);
            sides[BattleState.Player] = MakeSide(srt, "Player", playerSprite, new Vector2(66, 154), 122, 100, 112, 46, true);
            director = new AnimationDirector(this, sides, vfx, sound, animation, catalog);

            var msgInner = Ui.Box(srt, "MessageBox", 2, 156, 236, 58);
            message = new MessageBox(msgInner, font);

            var actInner = Ui.Box(srt, "ActionMenu", 144, 156, 94, 58);
            actionMenu = new ChoiceMenu(actInner.parent.gameObject, actInner, font,
                new[] { new Vector3(6, 8, 80), new Vector3(6, 28, 80) }, 1);
            actionMenu.Chosen += OnAction;
            actionMenu.Hide();

            var moveInner = Ui.Box(srt, "MoveMenu", 2, 156, 158, 58);
            moveMenu = new ChoiceMenu(moveInner.parent.gameObject, moveInner, font,
                new[] { new Vector3(4, 8, 76), new Vector3(78, 8, 76), new Vector3(4, 28, 76), new Vector3(78, 28, 76) }, 2);
            moveMenu.Chosen += OnMove;
            moveMenu.Cancelled += ShowActionMenu;
            moveMenu.Highlighted += ShowMoveInfo;
            moveMenu.Hide();
            var infoInner = Ui.Box(srt, "MoveInfo", 160, 156, 78, 58);
            moveInfoBox = infoInner.parent.gameObject;
            moveInfo = Ui.Label(infoInner, "Info", font, 5, 4, 68, 50);
            moveInfoBox.SetActive(false);

            var resInner = Ui.Box(srt, "ResultMenu", 96, 108, 142, 46);
            resultMenu = new ChoiceMenu(resInner.parent.gameObject, resInner, font,
                new[] { new Vector3(6, 6, 128), new Vector3(6, 24, 128) }, 1);
            resultMenu.Chosen += OnResult;
            resultMenu.Hide();

            vfx.BringToFront(); // 화면 번쩍임이 모든 것 위에 오도록

            foreach (var m in new[] { actionMenu, moveMenu, resultMenu })
            {
                m.Navigated += () => sound.Play(SfxKind.CursorMove);
                m.Chosen += _ => sound.Play(SfxKind.Confirm);
            }
            moveMenu.Cancelled += () => sound.Play(SfxKind.Cancel);
        }

        static (RectTransform rt, Image image, UiSpriteAnimator animator) MakeSprite(RectTransform screen, string name)
        {
            var rt = Ui.Rect(screen, name, 0, 0, 1, 1);
            rt.pivot = new Vector2(0.5f, 0f); // 발 위치 = 바닥 중앙
            var image = rt.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            var animator = rt.gameObject.AddComponent<UiSpriteAnimator>();
            return (rt, image, animator);
        }

        BattleSideView MakeSide(RectTransform screen, string name, (RectTransform rt, Image image, UiSpriteAnimator animator) sprite,
                                Vector2 feet, float bx, float by, float bw, float bh, bool player)
        {
            var inner = Ui.Box(screen, name + "Info", bx, by, bw, bh);
            var nameLabel = Ui.Label(inner, "Name", font, 4, 2, bw - 50, 14);
            Ui.Label(inner, "Level", font, bw - 42, 2, 36, 14, TextAlignmentOptions.TopRight).text = ":L50";
            var hp = new HpBar(inner, font, 6, 18, bw - 34, player);
            var view = new BattleSideView(sprite.rt, sprite.image, sprite.animator, feet, inner.parent.gameObject, nameLabel, hp, flashMaterial);
            view.Reset();
            return view;
        }

        // ------------------------------------------------------------------ 흐름
        void Update() => activeMenu?.Tick();

        IEnumerator NewBattle(Func<IReadOnlyList<BattleEvent>> start)
        {
            foreach (var v in sides) v.Reset();
            message.Clear();
            yield return PlayThenPrompt(start());
        }

        IEnumerator PlayThenPrompt(IReadOnlyList<BattleEvent> events)
        {
            SetMenu(null);
            yield return Play(events);
            if (session.State.IsOver) ShowResultMenu();
            else ShowActionMenu();
        }

        void SetMenu(ChoiceMenu menu)
        {
            foreach (var m in new[] { actionMenu, moveMenu, resultMenu }) if (m != menu) m.Hide();
            moveInfoBox.SetActive(menu == moveMenu);
            activeMenu = menu;
        }

        void ShowActionMenu()
        {
            // 행동 메뉴가 메시지 창 오른쪽을 덮으므로 그 왼쪽 폭(약 130px) 안에서 줄바꿈한다
            message.SetStatic(locale.Format(Keys.WhatToDo, names.Species(session.Player.Species.Id)), 130);
            SetMenu(actionMenu);
            actionMenu.Show(new[] { locale.Get(Keys.Fight), locale.Get(Keys.Run) });
        }

        void OnAction(int i)
        {
            if (i == 0) { ShowMoveMenu(); return; }
            SetMenu(null);
            StartCoroutine(RunAway());
        }

        IEnumerator RunAway()
        {
            var outcome = session.TryRun();
            yield return message.Say(locale.Get(BattleMessages.ForRun(outcome).Key));
            if (outcome == RunOutcome.CannotEscape) { ShowActionMenu(); yield break; }
            yield return NewBattle(session.NextBattle);
        }

        void ShowMoveMenu()
        {
            message.Clear();
            SetMenu(moveMenu);
            moveMenu.Show(session.PlayerMoveInfos.Select(m => locale.Get(m.NameKey)).ToArray(), lastMoveIndex);
        }

        void ShowMoveInfo(int i)
        {
            var infos = session.PlayerMoveInfos;
            if (i < 0 || i >= infos.Count) return;
            var m = infos[i].Move;
            moveInfo.text = $"{locale.Get(Keys.Type(m.Type))}\n{locale.Get(Keys.InfoPower)} {m.Power}\n{locale.Get(Keys.InfoAccuracy)} {m.Accuracy}";
        }

        void OnMove(int i)
        {
            lastMoveIndex = i;
            SetMenu(null);
            message.Clear();
            StartCoroutine(PlayThenPrompt(session.UseMove(i)));
        }

        void ShowResultMenu()
        {
            lastWin = session.State.Winner == BattleState.Player;
            SetMenu(resultMenu);
            resultMenu.Show(new[] { locale.Get(lastWin ? Keys.NextOpponent : Keys.Retry), locale.Get(Keys.ToTitle) });
        }

        void OnResult(int i)
        {
            SetMenu(null);
            if (i == 1)
            {
                root.SetActive(false);
                onExit?.Invoke();
                return;
            }
            StartCoroutine(NewBattle(lastWin ? session.NextBattle : (Func<IReadOnlyList<BattleEvent>>)session.Retry));
        }

        // ------------------------------------------------------------------ 이벤트 재생
        int SpeciesOf(int side) => session.State.Sides[side].Active.Species.Id;

        IEnumerator Play(IReadOnlyList<BattleEvent> events)
        {
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e)
                {
                    case PokemonSentOut s:
                    {
                        var view = sides[s.Side];
                        view.Name.text = names.Species(s.SpeciesId);
                        view.Hp.Set(s.Hp, s.MaxHp);
                        view.InfoBox.SetActive(false);
                        yield return director.SendOut(s.Side, catalog.GetSpecies(s.SpeciesId));
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
                        yield return director.UseMove(m.Side, SpeciesOf(m.Side), SpeciesOf(BattleState.Opponent(m.Side)),
                                                      catalog.GetMove(m.MoveId), hit, hpAnim);
                        if (hit != null || next is MoveMissed)
                        {
                            i++;
                            yield return SayAll(next); // "급소에 맞았다!" / "효과가 굉장했다!" / "공격은 빗나갔다!"
                        }
                        break;
                    }
                    case Fainted f:
                        yield return director.Faint(f.Side, catalog.GetSpecies(SpeciesOf(f.Side)));
                        yield return SayAll(e);
                        break;
                    case BattleEnded b:
                        sound.Play(b.WinnerSide == BattleState.Player ? SfxKind.Victory : SfxKind.Defeat);
                        if (b.WinnerSide == BattleState.Player) StartCoroutine(director.Victory(b.WinnerSide));
                        yield return SayAll(e);
                        break;
                    default:
                        yield return SayAll(e);
                        break;
                }
            }
        }

        IEnumerator SayAll(BattleEvent e)
        {
            foreach (var line in BattleMessages.For(e, names, SpeciesOf))
                yield return message.Say(locale.Format(line.Key, line.Args.Cast<object>().ToArray()));
        }
    }
}
