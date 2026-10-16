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
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 배틀 화면 (PROJECT.md 5.3): 게임보이식 레이아웃, 행동 메뉴(싸우다/도망) → 기술 2×2 → 턴 재생 → 결과 메뉴.
    /// 규칙은 판단하지 않는다. BattleSession 이 돌려준 BattleEvent 를 순서대로 "재생"만 한다(PROJECT.md 7.3).
    /// </summary>
    public sealed class BattleScreen : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset font;
        [SerializeField] TextAsset speciesJson;
        [SerializeField] TextAsset movesJson;
        [SerializeField] TextAsset rosterJson;
        [SerializeField] TextAsset localeKo;
        [SerializeField] TextAsset localeEn;
        [SerializeField] TextAsset localeJa;

        const int DemoPlayer = 25; // 피카츄
        const int DemoNpc = 9;     // 거북왕

        /// <summary>화면 문구 키. 단일 출처는 Application 의 BattleTextKeys (로케일 파일 검사도 그쪽 테스트가 한다).</summary>
        static class Keys
        {
            public const string Fight = BattleTextKeys.MenuFight;
            public const string Run = BattleTextKeys.MenuRun;
            public const string WhatToDo = BattleTextKeys.PromptWhatToDo;
            public const string NextOpponent = BattleTextKeys.ResultNextOpponent;
            public const string Retry = BattleTextKeys.ResultRetry;
            public const string ToTitle = BattleTextKeys.ResultTitle;
            public const string InfoType = BattleTextKeys.InfoType;
            public const string InfoPower = BattleTextKeys.InfoPower;
            public const string InfoAccuracy = BattleTextKeys.InfoAccuracy;
            public static string Type(PokemonType t) => BattleTextKeys.TypeName(t);
        }

        sealed class SideView
        {
            public RectTransform SpriteRect;
            public UiSpriteAnimator Animator;
            public Image Image;
            public Vector2 Home;
            public GameObject InfoBox;
            public TextMeshProUGUI Name;
            public HpBar Hp;
        }

        ContentCatalog catalog;
        LocaleTable locale;
        BattleNames names;
        BattleSession session;
        Action onExit;

        GameObject root;
        readonly SideView[] sides = new SideView[2];
        MessageBox message;
        ChoiceMenu actionMenu, moveMenu, resultMenu;
        TextMeshProUGUI moveInfo;
        GameObject moveInfoBox;
        ChoiceMenu activeMenu;
        SoundBank sound;
        bool lastWin;
        int lastMoveIndex;

        /// <summary>타이틀에서 호출. Demo 매치업(피카츄 vs 거북왕)을 시작한다. onExit 는 [처음으로].</summary>
        public void BeginDemo(string localeCode, Action onExit)
        {
            this.onExit = onExit;
            if (catalog == null) catalog = ContentLoader.Load(speciesJson.text, movesJson.text, rosterJson.text);
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

        /// <summary>새 판: 이전 판의 스프라이트·정보 상자를 치우고 등장부터 재생한다.</summary>
        IEnumerator NewBattle(Func<IReadOnlyList<BattleEvent>> start)
        {
            foreach (var v in sides)
            {
                v.Image.enabled = false;
                v.InfoBox.SetActive(false);
                v.SpriteRect.anchoredPosition = v.Home;
            }
            message.Clear();
            yield return PlayThenPrompt(start());
        }

        // ------------------------------------------------------------------ 레이아웃
        void Build()
        {
            var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PixelCanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            root = canvasGo;
            sound = new SoundBank(gameObject);

            // 240×216 화면을 캔버스 가운데에 둔다 (정수 배율은 PixelCanvasScaler)
            var screen = new GameObject("Screen", typeof(RectTransform), typeof(Image));
            screen.transform.SetParent(canvasGo.transform, false);
            var srt = (RectTransform)screen.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(PixelCanvasScaler.BaseWidth, PixelCanvasScaler.BaseHeight);
            screen.GetComponent<Image>().color = Ui.Background;
            screen.AddComponent<RectMask2D>(); // 연출이 화면 밖으로 나가지 않게

            // 발판 → 포켓몬 → 정보 상자 순으로 그린다 (나중에 만든 것이 위)
            Ui.Ellipse(srt, "NpcPlatform", 178, 82, 104, 18, Ui.Platform);
            Ui.Ellipse(srt, "PlayerPlatform", 66, 150, 108, 18, Ui.Platform);
            sides[BattleState.Npc] = MakeSide(srt, "Npc", new Vector2(178, 86), 6, 6, 116, 32, false);
            sides[BattleState.Player] = MakeSide(srt, "Player", new Vector2(66, 154), 122, 100, 112, 46, true);

            // 메시지 창
            var msgInner = Ui.Box(srt, "MessageBox", 2, 156, 236, 58);
            message = new MessageBox(msgInner, font);

            // 행동 메뉴 (메시지 창 오른쪽에 겹쳐 뜬다)
            var actInner = Ui.Box(srt, "ActionMenu", 144, 156, 94, 58);
            actionMenu = new ChoiceMenu(actInner.parent.gameObject, actInner, font,
                new[] { new Vector3(6, 8, 80), new Vector3(6, 28, 80) }, 1);
            actionMenu.Chosen += OnAction;
            actionMenu.Hide();

            // 기술 메뉴 2×2 + 정보 상자
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

            // 결과 메뉴
            var resInner = Ui.Box(srt, "ResultMenu", 96, 108, 142, 46);
            resultMenu = new ChoiceMenu(resInner.parent.gameObject, resInner, font,
                new[] { new Vector3(6, 6, 128), new Vector3(6, 24, 128) }, 1);
            resultMenu.Chosen += OnResult;
            resultMenu.Hide();

            foreach (var m in new[] { actionMenu, moveMenu, resultMenu })
            {
                m.Navigated += () => sound.Play(SfxKind.CursorMove);
                m.Chosen += _ => sound.Play(SfxKind.Confirm);
            }
            moveMenu.Cancelled += () => sound.Play(SfxKind.Cancel);
        }

        SideView MakeSide(RectTransform screen, string name, Vector2 feet, float bx, float by, float bw, float bh, bool player)
        {
            var spriteRt = Ui.Rect(screen, name + "Sprite", 0, 0, 1, 1);
            spriteRt.pivot = new Vector2(0.5f, 0f); // 발 위치 = 바닥 중앙
            var home = new Vector2(feet.x, -feet.y);
            spriteRt.anchoredPosition = home;
            var image = spriteRt.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            var animator = spriteRt.gameObject.AddComponent<UiSpriteAnimator>();

            var inner = Ui.Box(screen, name + "Info", bx, by, bw, bh);
            var nameLabel = Ui.Label(inner, "Name", font, 4, 2, bw - 50, 14);
            Ui.Label(inner, "Level", font, bw - 42, 2, 36, 14, TextAlignmentOptions.TopRight).text = ":L50";
            var hp = new HpBar(inner, font, 6, 18, bw - 34, player);
            return new SideView
            {
                SpriteRect = spriteRt, Animator = animator, Image = image, Home = home,
                InfoBox = inner.parent.gameObject, Name = nameLabel, Hp = hp,
            };
        }

        // ------------------------------------------------------------------ 흐름
        void Update() => activeMenu?.Tick();

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

        // ------------------------------------------------------------------ 이벤트 재생 (M3-2 에서 AnimationDirector 로 옮긴다)
        IEnumerator Play(IReadOnlyList<BattleEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case PokemonSentOut s:
                        yield return SendOut(s);
                        break;
                    case MoveUsed m:
                        yield return SayAll(e);
                        yield return Lunge(m.Side);
                        break;
                    case DamageDealt d:
                        if (d.Amount > 0) sound.Play(d.Critical ? SfxKind.CriticalHit : SfxKind.Hit);
                        yield return Hit(d);
                        yield return SayAll(e);
                        break;
                    case Fainted f:
                        sound.Play(SfxKind.Faint);
                        yield return Faint(f.Side);
                        yield return SayAll(e);
                        break;
                    case MoveMissed _:
                        sound.Play(SfxKind.Miss);
                        yield return SayAll(e);
                        break;
                    case BattleEnded b:
                        sound.Play(b.WinnerSide == BattleState.Player ? SfxKind.Victory : SfxKind.Defeat);
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
            foreach (var line in BattleMessages.For(e, names, side => session.State.Sides[side].Active.Species.Id))
                yield return message.Say(locale.Format(line.Key, line.Args.Cast<object>().ToArray()));
        }

        IEnumerator SendOut(PokemonSentOut s)
        {
            var view = sides[s.Side];
            var info = catalog.GetSpecies(s.SpeciesId);
            view.Animator.Play(SpriteSheet.Load(s.Side == BattleState.Player ? info.Sprites.Back : info.Sprites.Front));
            view.SpriteRect.anchoredPosition = view.Home;
            view.Name.text = names.Species(s.SpeciesId);
            view.Hp.Set(s.Hp, s.MaxHp);
            view.InfoBox.SetActive(false);
            // 등장: 바깥에서 미끄러져 들어온다 (NPC 는 오른쪽, 플레이어는 왼쪽)
            var from = view.Home + new Vector2(s.Side == BattleState.Npc ? 140 : -140, 0);
            yield return Slide(view, from, view.Home, 0.35f);
            view.InfoBox.SetActive(true);
            yield return SayAll(s);
        }

        IEnumerator Lunge(int side)
        {
            var view = sides[side];
            var dir = side == BattleState.Player ? new Vector2(10, 6) : new Vector2(-10, -6);
            yield return Slide(view, view.Home, view.Home + dir, 0.08f);
            yield return Slide(view, view.Home + dir, view.Home, 0.12f);
        }

        IEnumerator Hit(DamageDealt d)
        {
            var view = sides[d.TargetSide];
            if (d.Amount > 0)
                for (int i = 0; i < 3; i++)
                {
                    view.Image.enabled = false; yield return Wait(0.06f);
                    view.Image.enabled = true; yield return Wait(0.06f);
                }
            yield return view.Hp.AnimateTo(d.HpAfter);
        }

        IEnumerator Faint(int side)
        {
            var view = sides[side];
            yield return Slide(view, view.Home, view.Home + new Vector2(0, -48), 0.3f);
            view.Image.enabled = false;
            view.SpriteRect.anchoredPosition = view.Home;
        }

        static IEnumerator Slide(SideView view, Vector2 from, Vector2 to, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                var p = Vector2.Lerp(from, to, t / seconds);
                view.SpriteRect.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y)); // 정수 픽셀에 맞춘다
                yield return null;
            }
            view.SpriteRect.anchoredPosition = to;
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime) yield return null;
        }
    }
}
