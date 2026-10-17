using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Pokepedia.App.Battle;
using Pokepedia.Content;
using Pokepedia.Domain;
using Pokepedia.Domain.Battle;
using Pokepedia.Domain.Rules;
using TMPro;
using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 배틀 화면이 지금 무엇을 하는 중인지 (PROJECT.md 7.3). 입력은 Command·Result·Lab 에서만 받는다.
    /// 호스트 연결(init 대기)과 플레이어 포켓몬 정하기는 배틀 화면이 열리기 전 TitleScreen 이 맡는다.
    /// 플레이어 후보가 2마리 이상이 되는 확장 때 선택 화면(Selecting)을 Entering 앞에 더한다 (9.2).
    /// </summary>
    public enum BattleViewState
    {
        /// <summary>배틀 화면이 꺼져 있음(타이틀 등)</summary>
        Hidden,
        /// <summary>판 시작: VS 도입 장면과 등장 메시지</summary>
        Entering,
        /// <summary>행동·기술 메뉴에서 플레이어 입력을 기다림</summary>
        Command,
        /// <summary>한 턴(또는 도망)의 메시지·연출을 재생 중 — 입력 없음</summary>
        Animating,
        /// <summary>승패가 나서 결과 메뉴(다음 상대/다시 도전, 처음으로)를 기다림</summary>
        Result,
        /// <summary>도망칠 수 없는 배틀에서 "정말 도망칠까?"에 답을 기다림 (그만둔다 / 계속 싸운다)</summary>
        ConfirmQuit,
        /// <summary>플레이어가 쓰러져 [응원한다 / 트레이너가 처리한다]를 기다림 (5.3)</summary>
        ChooseFaint,
        /// <summary>연출 실험실 메뉴 (개발용)</summary>
        Lab,
    }

    /// <summary>
    /// 배틀 화면의 흐름 (PROJECT.md 5.3): 판 시작(VS·등장) → 행동 메뉴(싸우다/도망) → 기술 2×2 → 턴 재생 → 결과 메뉴.
    /// 규칙은 판단하지 않는다(BattleSession). 보이는 것은 BattleHud, 이벤트 재생은 BattleEventPlayer, 실험실은 BattleLab 이 맡고,
    /// 여기서는 표시 상태(BattleViewState)와 메뉴 사이의 전환만 다룬다.
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
        [SerializeField] Material flashMaterial;     // Pokepedia/Sprite Flash (피격 번쩍임, 본체 위에 겹치는 렌더러용)
        [SerializeField] Material particleMaterial;  // 파티클·월드의 조명 안 받는 스프라이트 (URP 2D Sprite-Unlit)
        [SerializeField] Material litMaterial;       // 월드의 조명 받는 스프라이트: 배경·발판·포켓몬 (URP 2D Sprite-Lit)

        const int DemoPlayer = 25; // 피카츄
        const int DemoNpc = 9;     // 거북왕

        /// <summary>화면 문구 키. 단일 출처는 Application 의 BattleTextKeys (로케일 파일 검사도 그쪽 테스트가 한다).</summary>
        static class Keys
        {
            public const string Fight = BattleTextKeys.MenuFight;
            public const string Run = BattleTextKeys.MenuRun;
            public const string WhatToDo = BattleTextKeys.PromptWhatToDo;
            public const string NextOpponent = BattleTextKeys.ResultNextOpponent;
            public const string Rematch = BattleTextKeys.ResultRematch;
            public const string Retry = BattleTextKeys.ResultRetry;
            public const string ToTitle = BattleTextKeys.ResultTitle;
            public const string InfoPower = BattleTextKeys.InfoPower;
            public const string InfoAccuracy = BattleTextKeys.InfoAccuracy;
            public static string Type(PokemonType t) => BattleTextKeys.TypeName(t);
        }

        readonly BattleContext ctx = new BattleContext();
        BattleHud hud;
        BattleEventPlayer player;
        BattleLab lab;
        Action onExit;
        Action<bool> onBattleEnded;   // Collection: 한 판의 승패를 호스트에 알린다 (battleEnded, 9.2)
        ChoiceMenu activeMenu;
        bool lastWin;
        bool oneOpponent;   // 상대 후보가 1마리: 이겨도 같은 상대라 [다음 상대] 대신 [한 번 더 싸운다]
        int lastMoveIndex;
        int runPresses;     // 이번 판에서 "도망"을 누른 횟수: 두 번째부터 그만둘지 묻는다

        /// <summary>지금 무엇을 하는 중인지. 입력 처리기는 맞는 상태일 때만 동작한다(중복 입력·연출 중 입력 방지).</summary>
        public BattleViewState State { get; private set; } = BattleViewState.Hidden;

        /// <summary>타이틀에서 호출. Demo 매치업(피카츄 vs 거북왕)을 시작한다. onExit 는 [처음으로].</summary>
        public void BeginDemo(string localeCode, Action onExit)
        {
            Prepare(localeCode, onExit);
            // 누구나 처음 하는 전투라 져서 다시 도전할 이유가 없다: 쓰러지면 응원해 다시 일으킬지, 트레이너가 처리해 끝낼지 고른다 (5.3)
            ctx.Session = new BattleSession(ctx.Catalog, BattleRules.Default, NewRandom(), BattleMode.Demo, DemoPlayer, () => DemoNpc, faintChoice: true);
            oneOpponent = true;
            StartCoroutine(NewBattle(ctx.Session.Start));
        }

        /// <summary>
        /// Pokepedia 에서 들어온 Collection 모드 (9.2): 고른 플레이어 포켓몬으로, 상대는 roster.json 가중 추첨.
        /// 상대 후보가 1마리뿐이면 도망을 끈다. onBattleEnded 는 승패가 날 때마다 (true = 승리).
        /// </summary>
        public void BeginCollection(string localeCode, PlayerChoice choice, Action onExit, Action<bool> onBattleEnded)
        {
            Prepare(localeCode, onExit);
            this.onBattleEnded = onBattleEnded;
            ctx.PlayerShiny = choice.Shiny;
            var random = NewRandom();
            bool canEscape = OpponentPicker.CandidateCount(ctx.Catalog, choice.SpeciesId) > 1;
            oneOpponent = !canEscape;
            ctx.Session = new BattleSession(ctx.Catalog, BattleRules.Default, random, BattleMode.Collection, choice.SpeciesId,
                                            OpponentPicker.Weighted(ctx.Catalog, random, choice.SpeciesId), canEscape,
                                            faintChoice: true);   // Pokepedia 에서 피카츄 띠부씰로 들어오는 첫 전투도 같은 경험 (5.3)
            StartCoroutine(NewBattle(ctx.Session.Start));
        }

        /// <summary>연출 실험실 (주소에 ?lab, 또는 타이틀에서 L). 자세한 것은 BattleLab.</summary>
        public void BeginLab(string localeCode, Action onExit)
        {
            Prepare(localeCode, onExit);
            ctx.Session = new BattleSession(ctx.Catalog, BattleRules.Default, NewRandom(), BattleMode.Demo, DemoPlayer, () => DemoNpc);
            State = BattleViewState.Animating;
            StartCoroutine(lab.Intro());
        }

        /// <summary>Collection 모드의 플레이어 후보 (보유 ∩ 플레이어 가능). 타이틀이 연결 직후 묻는다.</summary>
        public IReadOnlyList<PlayerChoice> CollectionPlayers(IEnumerable<App.Host.OwnedSticker> owned)
        {
            LoadCatalog();
            return CollectionLineup.Players(ctx.Catalog, owned);
        }

        /// <summary>플레이어로 쓸 수 있는 포켓몬의 이름 키 (보유 0마리일 때 "무엇을 얻으면 되는지" 안내용)</summary>
        public IEnumerable<string> PlayableNameKeys()
        {
            LoadCatalog();
            return ctx.Catalog.Roster.Where(r => r.Playable).Select(r => ctx.Catalog.GetSpecies(r.Id).NameKey);
        }

        void LoadCatalog()
        {
            if (ctx.Catalog != null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ctx.Catalog = ContentLoader.Load(speciesJson.text, movesJson.text, rosterJson.text);
            Perf("content", sw);
            ctx.Animation = AnimationCatalog.Parse(animationJson.text);
            Perf("animation", sw);
        }

        static SeededRandom NewRandom() => new SeededRandom((uint)Environment.TickCount | 1u);

        void Prepare(string localeCode, Action onExit)
        {
            this.onExit = onExit;
            onBattleEnded = null;
            ctx.PlayerShiny = false;
            LoadCatalog();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var asset = localeCode switch { "en" => localeEn, "ja" => localeJa, _ => localeKo };
            ctx.Locale = LocaleTable.Parse(asset.text);
            Perf("locale", sw);
            var catalog = ctx.Catalog; var locale = ctx.Locale;
            ctx.Names = new BattleNames(id => locale.Get(catalog.GetSpecies(id).NameKey), id => locale.Get(catalog.GetMove(id).NameKey));
            if (hud == null) { Build(); Perf("build ui", sw); }
            hud.SetVisible(true);
            lastMoveIndex = 0;
        }

        /// <summary>배틀 시작 단계별 누적 시간을 콘솔에 남긴다 (WebGL 시작 지연 조사용, 처음 한 번만 몇 줄)</summary>
        static void Perf(string step, System.Diagnostics.Stopwatch sw) => Debug.Log($"[Perf] {step}: {sw.ElapsedMilliseconds} ms");

        void Build()
        {
            hud = new BattleHud(this, font, flashMaterial, particleMaterial, litMaterial, ctx.Catalog, ctx.Animation);
            player = new BattleEventPlayer(this, hud, ctx);
            lab = new BattleLab(this, hud, ctx, player, SetMenu,
                playing => State = playing ? BattleViewState.Animating : BattleViewState.Lab, Exit);

            hud.ActionMenu.Chosen += OnAction;
            hud.MoveMenu.Chosen += OnMove;
            hud.MoveMenu.Cancelled += ShowActionMenu;
            hud.ResultMenu.Chosen += OnResult;
            hud.QuitMenu.Chosen += OnQuitChoice;
            hud.QuitMenu.Cancelled += () => OnQuitChoice(1);
            hud.FaintMenu.Chosen += OnFaintChoice;   // 취소(바깥 누르기·Esc)는 없다: 둘 중 하나를 골라야 이어진다
        }

        // ------------------------------------------------------------------ 흐름
        void Update() => activeMenu?.Tick();

        IEnumerator NewBattle(Func<IReadOnlyList<BattleEvent>> start)
        {
            State = BattleViewState.Entering;
            runPresses = 0;
            player.StopVictory();
            hud.ResetStage();
            var events = start();   // 새 상대가 정해진 뒤에 VS 장면을 그린다
            yield return player.PlayVs();
            yield return PlayThenPrompt(events);
        }

        IEnumerator PlayThenPrompt(IReadOnlyList<BattleEvent> events)
        {
            SetMenu(null);
            yield return player.Play(events);
            if (ctx.Session.AwaitingFaintChoice) ShowFaintChoice();
            else if (ctx.Session.State.IsOver) ShowResultMenu();
            else ShowActionMenu();
        }

        void SetMenu(ChoiceMenu menu)
        {
            foreach (var m in new[] { hud.ActionMenu, hud.MoveMenu, hud.ResultMenu, hud.QuitMenu, hud.FaintMenu }) if (m != menu) m.Hide();
            activeMenu = menu;
        }

        void ShowActionMenu()
        {
            State = BattleViewState.Command;
            // 행동 메뉴가 메시지 창 오른쪽을 덮으므로 그 왼쪽 폭(약 130px) 안에서 줄바꿈한다
            hud.Message.SetStatic(ctx.Locale.Format(Keys.WhatToDo, ctx.Names.Species(ctx.Session.Player.Species.Id)), 130);
            SetMenu(hud.ActionMenu);
            hud.ActionMenu.Show(new[] { ctx.Locale.Get(Keys.Fight), ctx.Locale.Get(Keys.Run) });
        }

        void OnAction(int i)
        {
            if (State != BattleViewState.Command) return;
            if (i == 0) { ShowMoveMenu(); return; }
            State = BattleViewState.Animating;
            SetMenu(null);
            StartCoroutine(RunAway());
        }

        IEnumerator RunAway()
        {
            var outcome = ctx.Session.TryRun();
            // 도망칠 수 없는 배틀(Demo, 상대 1마리)에 갇힌 느낌이 들지 않게: 두 번째 "도망"부터는 배틀을 그만둘지 묻는다 (5.3)
            if (outcome == RunOutcome.CannotEscape && ++runPresses >= 2) { ShowQuitConfirm(); yield break; }
            yield return hud.Message.Say(ctx.Locale.Get(BattleMessages.ForRun(outcome).Key));
            if (outcome == RunOutcome.CannotEscape) { ShowActionMenu(); yield break; }
            yield return hud.Sound.FadeOutMusic(0.3f);
            yield return NewBattle(ctx.Session.NextBattle);
        }

        void ShowMoveMenu()
        {
            hud.Message.Clear();
            SetMenu(hud.MoveMenu);
            hud.Moves.SetMoves(ctx.Session.PlayerMoveInfos, ctx.Locale.Get(Keys.InfoPower), ctx.Locale.Get(Keys.InfoAccuracy));
            hud.MoveMenu.Show(ctx.Session.PlayerMoveInfos.Select(m => ctx.Locale.Get(m.NameKey)).ToArray(), lastMoveIndex);
            hud.Moves.FitNames();
        }

        void OnMove(int i)
        {
            if (State != BattleViewState.Command) return;
            State = BattleViewState.Animating;
            lastMoveIndex = i;
            StartCoroutine(PressThenUse(i));
        }

        /// <summary>누른 기술 버튼이 살짝 눌렸다 튀어 오른 뒤 메뉴를 닫고 턴을 진행한다</summary>
        IEnumerator PressThenUse(int i)
        {
            yield return hud.Moves.Press(i);
            SetMenu(null);
            hud.Message.Clear();
            yield return PlayThenPrompt(ctx.Session.UseMove(i));
        }

        void ShowResultMenu()
        {
            State = BattleViewState.Result;
            lastWin = ctx.Session.State.Winner == BattleState.Player;
            onBattleEnded?.Invoke(lastWin);
            SetMenu(hud.ResultMenu);
            var again = !lastWin ? Keys.Retry : oneOpponent ? Keys.Rematch : Keys.NextOpponent;
            hud.ResultMenu.Show(new[] { ctx.Locale.Get(again), ctx.Locale.Get(Keys.ToTitle) });
        }

        void ShowQuitConfirm()
        {
            State = BattleViewState.ConfirmQuit;
            hud.Message.SetStatic(ctx.Locale.Get(BattleTextKeys.QuitAsk));   // 확인 메뉴는 메시지 창 위에 떠서 창 전체 폭을 쓴다
            SetMenu(hud.QuitMenu);
            hud.QuitMenu.Show(new[] { ctx.Locale.Get(BattleTextKeys.QuitYes), ctx.Locale.Get(BattleTextKeys.QuitNo) }, 1);   // 기본 커서는 "계속 싸운다"
        }

        void OnQuitChoice(int i)
        {
            if (State != BattleViewState.ConfirmQuit) return;
            SetMenu(null);
            if (i == 0) { Exit(); return; }
            ShowActionMenu();
        }

        /// <summary>쓰러진 뒤: [○○을(를) 응원한다]면 다시 일어나 이어 싸우고, [트레이너가 처리한다]면 몬스터볼로 상대를 쓰러뜨려 이긴다 (5.3)</summary>
        void ShowFaintChoice()
        {
            State = BattleViewState.ChooseFaint;
            hud.Message.SetStatic(ctx.Locale.Get(BattleTextKeys.FaintAsk));
            SetMenu(hud.FaintMenu);
            var name = ctx.Names.Species(ctx.Session.Player.Species.Id);
            hud.FaintMenu.Show(new[] { ctx.Locale.Format(BattleTextKeys.FaintCheer, name), ctx.Locale.Get(BattleTextKeys.FaintFinish) }, 0);
        }

        void OnFaintChoice(int i)
        {
            if (State != BattleViewState.ChooseFaint) return;
            State = BattleViewState.Animating;
            hud.Message.Clear();
            StartCoroutine(PlayThenPrompt(i == 0 ? ctx.Session.Cheer() : ctx.Session.TrainerFinish()));
        }

        void OnResult(int i)
        {
            if (State != BattleViewState.Result) return;
            SetMenu(null);
            if (i == 1) { Exit(); return; }
            StartCoroutine(NewBattle(lastWin ? ctx.Session.NextBattle : (Func<IReadOnlyList<BattleEvent>>)ctx.Session.Retry));
        }

        /// <summary>배틀 화면을 닫고 타이틀로 (결과 메뉴 [처음으로], 실험실 취소)</summary>
        void Exit()
        {
            State = BattleViewState.Hidden;
            hud.Sound.StopMusic();
            hud.SetVisible(false);
            onExit?.Invoke();
        }
    }
}
