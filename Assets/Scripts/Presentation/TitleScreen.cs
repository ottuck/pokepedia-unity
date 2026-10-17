using System;
using System.Linq;
using Pokepedia.App.Battle;
using Pokepedia.App.Host;
using Pokepedia.Bridge;
using Pokepedia.Content;
using Pokepedia.Presentation.Battle;
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pokepedia.Presentation
{
    /// <summary>
    /// 진입 화면: 시작(오디오 시작) → 모드 판별(Demo / Pokepedia 연결) → 배틀 화면으로.
    /// 연결되면 보유 ∩ 플레이어 가능(CollectionLineup)으로 바로 Collection 배틀을 연다. 후보가 없으면 무엇을 얻으면 되는지와 [데모 체험]을 보여 준다 (9.2).
    /// <para>
    /// 화면은 WebGL 빌드에서는 웹 템플릿(HTML)이 그린다: 매끄러운 글꼴·그라데이션·아트워크로 레트로 도트는 VS 화면부터만 나온다
    /// (2026-10-17 사용자 피드백 "도트 시작 화면이 촌스럽다"). 이 클래스는 상태와 문구(로케일 파일)만 보내고(HostBridge.ShowTitle),
    /// 버튼은 템플릿이 SendMessage("App", "OnTitleAction", ...) 로 돌려준다 — 클릭 처리 안에서 바로 불리므로 첫 소리도 막히지 않는다.
    /// 에디터에는 HTML 이 없어 단순한 Unity 패널로 대신한다.
    /// </para>
    /// 규칙 판단은 Application(HostHandshake·CollectionLineup)에 맡기고 여기서는 화면과 입력만 다룬다.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] HostBridge bridge;
        [SerializeField] BattleScreen battle;
        [SerializeField] TMP_FontAsset font;
        [SerializeField] TextAsset localeKo;
        [SerializeField] TextAsset localeEn;
        [SerializeField] TextAsset localeJa;

        /// <summary>HTML 시작 화면에 보내는 상태 (JsonUtility 로 직렬화, 필드 이름이 템플릿과의 약속)</summary>
        [Serializable]
        sealed class TitleView
        {
            public string view;      // idle | connecting | failed | no_partner | hidden
            public string message;   // 연결 중·실패·띠부씰 없음 안내
            public string start, retry, demo, footer;
        }

        HostHandshake handshake;
        LocaleTable locale;
        string localeCode = "ko";
        bool visible = true;
        bool atStart = true;   // 시작 버튼을 기다린다 (처음, 그리고 배틀에서 [처음으로]로 돌아온 뒤)
        AudioSource sfx;
        AudioClip confirmClip;
        bool labRequested;   // ?lab 또는 타이틀에서 L: 연출 실험실
        bool noPartner;      // 연결됐지만 플레이어로 쓸 포켓몬이 없다

        // 에디터 대체 화면 (HTML 이 없을 때만)
        GameObject canvasRoot;
        TextMeshProUGUI status;
        Button startButton, retryButton, demoButton;
        TextMeshProUGUI startLabel, retryLabel, demoLabel;

        bool Html => bridge != null && bridge.HasHtmlTitle;

        void Start()
        {
            locale = LocaleTable.Parse(localeKo.text);
            // 첫 화면부터 페이지 언어로: ?lang(Pokepedia) → 없으면 브라우저 언어. 연결 뒤에는 init.locale 이 다시 정한다.
            SwitchLocale(bridge != null && bridge.Lang != null ? bridge.Lang : SystemLocale());
            handshake = new HostHandshake(bridge);
            handshake.StateChanged += OnStateChanged;
            labRequested = bridge != null && bridge.IsLabMode;
            if (bridge != null && bridge.IsFpsMode) FpsOverlay.Create(font);
            sfx = gameObject.AddComponent<AudioSource>();
            confirmClip = Beep(880f, 0.07f);
            if (!Html) BuildFallbackUi();
            Refresh();
        }

        void Update()
        {
            handshake?.Tick(Time.realtimeSinceStartupAsDouble);
            var kb = Keyboard.current;
            if (kb == null || !visible || !atStart) return;
            if (kb.lKey.wasPressedThisFrame) { labRequested = true; OnStart(); return; }
            if (kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) OnStart();
        }

        /// <summary>HTML 시작 화면의 버튼 (템플릿이 SendMessage 로 부른다): start | retry | demo</summary>
        public void OnTitleAction(string action)
        {
            if (!visible) return;
            switch (action)
            {
                case "start" when atStart: OnStart(); break;
                case "retry" when handshake.State == LaunchState.TimedOut: OnRetry(); break;
                case "demo" when handshake.State == LaunchState.TimedOut || noPartner: OnDemo(); break;
            }
        }

        void OnStart()
        {
            // 브라우저 자동 재생 정책: 첫 소리는 사용자 입력 처리 안에서 내야 오디오가 열린다.
            sfx.PlayOneShot(confirmClip);
            atStart = false;
            if (labRequested)
            {
                Hide();
                battle.BeginLab(localeCode, BackToTitle);
                return;
            }
            handshake.Begin(Time.realtimeSinceStartupAsDouble);
        }

        void OnRetry()
        {
            sfx.PlayOneShot(confirmClip);
            handshake.Retry(Time.realtimeSinceStartupAsDouble);
        }

        void OnDemo()
        {
            sfx.PlayOneShot(confirmClip);
            StartDemoBattle();
        }

        void OnStateChanged(LaunchState s)
        {
            noPartner = false;
            if (s == LaunchState.Connected)
            {
                if (!string.IsNullOrEmpty(handshake.Init.locale)) SwitchLocale(handshake.Init.locale);
                var players = battle.CollectionPlayers(handshake.Init.owned);
                // 후보가 여럿이 되면 선택 화면을 여기에 둔다. 지금은 플레이어 가능이 1마리라 첫 후보로 바로 간다.
                if (players.Count > 0) { StartCollectionBattle(players[0]); return; }
                noPartner = true;
            }
            Refresh();
            if (s == LaunchState.Demo) StartDemoBattle();
        }

        void StartCollectionBattle(PlayerChoice choice)
        {
            Hide();
            battle.BeginCollection(localeCode, choice, BackToTitle, win => bridge.Post(HostProtocol.BattleEndedJson(win)));
        }

        void StartDemoBattle()
        {
            Hide();
            battle.BeginDemo(localeCode, BackToTitle);
        }

        void Hide()
        {
            visible = false;
            if (Html) bridge.ShowTitle(JsonUtility.ToJson(new TitleView { view = "hidden" }));
            else canvasRoot.SetActive(false);
        }

        /// <summary>배틀에서 [처음으로]·실험실 나가기: 처음 상태(시작 버튼)로</summary>
        void BackToTitle()
        {
            visible = true;
            atStart = true;
            noPartner = false;
            if (!Html) canvasRoot.SetActive(true);
            Refresh();
        }

        static string SystemLocale() => Application.systemLanguage switch
        {
            SystemLanguage.Korean => "ko",
            SystemLanguage.Japanese => "ja",
            SystemLanguage.Unknown => "ko",   // 알 수 없으면 지금까지처럼 한국어
            _ => "en",
        };

        void SwitchLocale(string code)
        {
            var asset = code switch { "en" => localeEn, "ja" => localeJa, _ => localeKo };
            if (asset == null) return;
            locale = LocaleTable.Parse(asset.text);
            localeCode = code == "en" || code == "ja" ? code : "ko";
        }

        void Refresh()
        {
            var s = handshake.State;
            var view = atStart ? "idle" : s switch
            {
                LaunchState.WaitingInit => "connecting",
                LaunchState.TimedOut => "failed",
                LaunchState.Connected when noPartner => "no_partner",
                _ => "connecting",   // Demo·Connected 는 곧 배틀로 넘어간다
            };
            var message = view switch
            {
                "connecting" => locale.Get("title.connecting"),
                "failed" => locale.Get("title.connect_failed"),
                "no_partner" => locale.Format("title.no_partner", string.Join(", ", battle.PlayableNameKeys().Select(locale.Get))),
                _ => "",
            };
            var v = new TitleView
            {
                view = view, message = message,
                start = locale.Get("title.start"), retry = locale.Get("title.retry"), demo = locale.Get("title.try_demo"),
                footer = locale.Get("title.footer"),
            };
            if (Html) { bridge.ShowTitle(JsonUtility.ToJson(v)); return; }

            startButton.gameObject.SetActive(view == "idle");
            retryButton.gameObject.SetActive(view == "failed");
            demoButton.gameObject.SetActive(view == "failed" || view == "no_partner");
            ((RectTransform)demoButton.transform).anchoredPosition = new Vector2(view == "failed" ? 50 : 0, -52);
            startLabel.text = v.start; retryLabel.text = v.retry; demoLabel.text = v.demo;
            status.text = message;
        }

        /// <summary>에디터용 대체 화면: 제목·안내·버튼만 (WebGL 빌드는 HTML 시작 화면)</summary>
        void BuildFallbackUi()
        {
            canvasRoot = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PixelCanvasScaler));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            var root = UiFactory.Panel(canvasRoot.transform, "Screen", UiFactory.Lightest);
            UiFactory.Text(root, "Title", font, new Vector2(0, 56), new Vector2(230, 40), 24).text = "Pokepedia " + locale.Get("title.subtitle");
            status = UiFactory.Text(root, "Status", font, new Vector2(0, 2), new Vector2(220, 40), 12);
            startButton = UiFactory.Button(root, "Start", font, new Vector2(0, -52), new Vector2(80, 22), OnStart, out startLabel);
            retryButton = UiFactory.Button(root, "Retry", font, new Vector2(-50, -52), new Vector2(90, 22), OnRetry, out retryLabel);
            demoButton = UiFactory.Button(root, "Demo", font, new Vector2(50, -52), new Vector2(90, 22), OnDemo, out demoLabel);
        }

        static AudioClip Beep(float freq, float seconds)
        {
            int rate = 22050, n = (int)(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * freq * i / rate)) * 0.15f * (1f - i / (float)n);
            var c = AudioClip.Create("confirm", n, 1, rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
