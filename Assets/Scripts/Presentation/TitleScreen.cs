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
    /// 진입 화면: START(오디오 시작) → 모드 판별(Demo / Pokepedia 연결) → Demo 면 배틀 화면으로.
    /// 규칙 판단은 Application(HostHandshake)에 맡기고 여기서는 화면과 입력만 다룬다.
    /// Collection 모드(연결됨)의 포켓몬 선택 화면은 M6.
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

        void Start()
        {
            locale = LocaleTable.Parse(localeKo.text);
            handshake = new HostHandshake(bridge);
            handshake.StateChanged += OnStateChanged;
            labRequested = bridge != null && bridge.IsLabMode;
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
            if (s == LaunchState.Connected && !string.IsNullOrEmpty(handshake.Init.locale)) SwitchLocale(handshake.Init.locale);
            Refresh();
            if (s == LaunchState.Demo) StartDemoBattle();
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
            demoButton.gameObject.SetActive(s == LaunchState.TimedOut);
            startLabel.text = locale.Get("title.start");
            retryLabel.text = locale.Get("title.retry");
            demoLabel.text = locale.Get("title.try_demo");
            status.text = s switch
            {
                LaunchState.WaitingInit => locale.Get("title.connecting"),
                LaunchState.TimedOut => locale.Get("title.connect_failed"),
                LaunchState.Connected => locale.Format("title.connected", handshake.Init.owned?.Length ?? 0),
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
