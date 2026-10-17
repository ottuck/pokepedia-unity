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
    /// 진입 화면: START(오디오 시작) → 모드 판별(Demo / Pokepedia 연결) → 배틀 화면으로.
    /// 연결되면 보유 ∩ 플레이어 가능(CollectionLineup)으로 바로 Collection 배틀을 연다. 후보가 없으면 무엇을 얻으면 되는지와 [데모 체험]을 보여 준다 (9.2).
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

        HostHandshake handshake;
        LocaleTable locale;
        string localeCode = "ko";
        GameObject canvasRoot;
        TextMeshProUGUI status;
        Button startButton, retryButton, demoButton;
        TextMeshProUGUI startLabel, retryLabel, demoLabel;
        AudioSource sfx;
        AudioClip confirmClip;
        bool labRequested;   // ?lab 또는 타이틀에서 L: 연출 실험실
        bool noPartner;      // 연결됐지만 플레이어로 쓸 포켓몬이 없다

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
            BuildUi();
            Refresh();
        }

        void BuildUi()
        {
            canvasRoot = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PixelCanvasScaler));
            canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            if (FindAnyObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var root = UiFactory.Panel(canvasRoot.transform, "Screen", UiFactory.Lightest);
            UiFactory.Text(root, "Title", font, new Vector2(0, 56), new Vector2(230, 40), 24).text = locale.Get("title.name");
            status = UiFactory.Text(root, "Status", font, new Vector2(0, 2), new Vector2(220, 40), 12);
            startButton = UiFactory.Button(root, "Start", font, new Vector2(0, -52), new Vector2(80, 22), OnStart, out startLabel);
            retryButton = UiFactory.Button(root, "Retry", font, new Vector2(-50, -52), new Vector2(90, 22), OnRetry, out retryLabel);
            demoButton = UiFactory.Button(root, "Demo", font, new Vector2(50, -52), new Vector2(90, 22), OnDemo, out demoLabel);
        }

        void Update()
        {
            handshake?.Tick(Time.realtimeSinceStartupAsDouble);
            var kb = Keyboard.current;
            if (kb == null || !canvasRoot.activeSelf) return;
            if (kb.lKey.wasPressedThisFrame && startButton.gameObject.activeSelf) { labRequested = true; OnStart(); return; }
            if ((kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) && startButton.gameObject.activeSelf) OnStart();
        }

        void OnStart()
        {
            // 브라우저 자동 재생 정책: 첫 소리는 사용자 입력 처리 안에서 내야 오디오가 열린다.
            sfx.PlayOneShot(confirmClip);
            if (labRequested)
            {
                canvasRoot.SetActive(false);
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
            canvasRoot.SetActive(false);
            battle.BeginCollection(localeCode, choice, BackToTitle, win => bridge.Post(HostProtocol.BattleEndedJson(win)));
        }

        void StartDemoBattle()
        {
            canvasRoot.SetActive(false);
            battle.BeginDemo(localeCode, BackToTitle);
        }

        void BackToTitle()
        {
            canvasRoot.SetActive(true);
            startButton.gameObject.SetActive(true);
            retryButton.gameObject.SetActive(false);
            demoButton.gameObject.SetActive(false);
            status.text = "";
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
            startButton.gameObject.SetActive(s == LaunchState.Idle);
            retryButton.gameObject.SetActive(s == LaunchState.TimedOut);
            demoButton.gameObject.SetActive(s == LaunchState.TimedOut || noPartner);
            // [다시 시도]와 나란히 있을 때만 오른쪽, 혼자면 가운데
            ((RectTransform)demoButton.transform).anchoredPosition = new Vector2(s == LaunchState.TimedOut ? 50 : 0, -52);
            startLabel.text = locale.Get("title.start");
            retryLabel.text = locale.Get("title.retry");
            demoLabel.text = locale.Get("title.try_demo");
            status.text = s switch
            {
                LaunchState.WaitingInit => locale.Get("title.connecting"),
                LaunchState.TimedOut => locale.Get("title.connect_failed"),
                LaunchState.Connected when noPartner => locale.Format("title.no_partner", string.Join(", ", battle.PlayableNameKeys().Select(locale.Get))),
                _ => "",
            };
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
