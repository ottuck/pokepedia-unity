using System.Collections.Generic;
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
    /// 진입 화면: PRESS START(오디오 시작) → 모드 판별(Demo / Pokepedia 연결) → 배틀 화면으로.
    /// 연결되면 보유 ∩ 플레이어 가능(CollectionLineup)으로 바로 Collection 배틀을 연다. 후보가 없으면 무엇을 얻으면 되는지와 [데모 체험]을 보여 준다 (9.2).
    /// 모양은 Pokepedia 퀴즈의 시작 화면(intro.module.css)을 따른다: 계단식 하늘, 뭉게구름, 반짝이, 두꺼운 도트 테두리 로고,
    /// 깜빡이는 ▶ PRESS START, 화면 어디를 눌러도 시작. 하늘에는 피카츄와 거북왕의 그림자가 마주 선다. 그림은 모두 코드로 그린다.
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

        // Pokepedia 퀴즈 시작 화면과 같은 색
        static readonly Color SkyTop = Hex(0x7cc4ff), SkyMid = Hex(0x9fd5ff), SkyLow = Hex(0xd8efff), CloudRim = Hex(0xb9defc);
        static readonly Color LogoFill = Hex(0xc86cff), LogoAccent = Hex(0xffd23f), LogoLine = Hex(0x3b1466);
        static readonly Color SubRed = Hex(0xe3342f), SubDark = Hex(0x7a1411), Ink = Hex(0x1f1d2e), Spark = Hex(0xffe066);
        static readonly Color Shadow = new Color(0.12f, 0.12f, 0.2f, 0.78f);
        const int CloudHeight = 64;
        const float FeetY = -48;

        HostHandshake handshake;
        LocaleTable locale;
        string localeCode = "ko";
        GameObject canvasRoot, statusBox;
        TextMeshProUGUI status, pressStart, footer;
        TextMeshProUGUI[] subtitle;
        Button startButton, retryButton, demoButton;
        TextMeshProUGUI retryLabel, demoLabel;
        RectTransform[] silhouettes;
        Image[] sparkles;
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

            var root = UiFactory.Panel(canvasRoot.transform, "Screen", Color.white);
            root.GetComponent<Image>().sprite = SkySprite();

            // 하늘: 반짝이 → 피카츄(왼쪽, 오른쪽을 보게 뒤집음)와 거북왕의 그림자 → 아래쪽 뭉게구름
            sparkles = new[] { new Vector2(-98, 8), new Vector2(-74, -30), new Vector2(-12, 12), new Vector2(16, -24), new Vector2(86, 10), new Vector2(102, -32) }
                .Select((p, i) => Sparkle(root, p, i)).ToArray();
            var (player, npc) = battle.DemoFronts();
            silhouettes = new[] { Silhouette(root, player, -58, true), Silhouette(root, npc, 56, false) };
            Picture(root, "Clouds", CloudSprite(), new Vector2(0, -(PixelCanvasScaler.BaseHeight - CloudHeight) / 2f));

            // 로고: 진한 보라 테두리 2px(8방향 복사) + 오른쪽 아래 흰 그림자, "pedia" 는 노란색 (퀴즈 로고와 같은 짜임)
            Outlined(root, "Logo", "Poke<color=#FFD23F>pedia</color>", "Pokepedia", new Vector2(0, 62), 36, LogoFill, LogoLine, 2, 4);
            subtitle = Outlined(root, "Subtitle", "", "", new Vector2(0, 30), 12, SubRed, SubDark, 0, 1);
            foreach (var t in subtitle) t.characterSpacing = 30;

            // 화면 어디를 눌러도 시작 (퀴즈와 같음). 다시 시도·데모 버튼보다 먼저 만들어 그 아래에 깐다.
            var tap = new GameObject("TapToStart", typeof(RectTransform), typeof(Image), typeof(Button));
            tap.transform.SetParent(root, false);
            ((RectTransform)tap.transform).sizeDelta = new Vector2(PixelCanvasScaler.BaseWidth, PixelCanvasScaler.BaseHeight);
            tap.GetComponent<Image>().color = Color.clear;
            startButton = tap.GetComponent<Button>();
            startButton.transition = Selectable.Transition.None;
            startButton.onClick.AddListener(OnStart);

            pressStart = Label(root, "PressStart", new Vector2(0, -66), 12);
            footer = Label(root, "Footer", new Vector2(0, -96), 12);

            statusBox = Box(root, "StatusBox", new Vector2(0, -8), new Vector2(224, 44));
            status = UiFactory.Text(statusBox.transform, "Status", font, Vector2.zero, new Vector2(216, 40), 12);
            status.color = Ink;
            retryButton = StyledButton(root, "Retry", new Vector2(-50, -62), OnRetry, out retryLabel);
            demoButton = StyledButton(root, "Demo", new Vector2(50, -62), OnDemo, out demoLabel);
        }

        void Update()
        {
            handshake?.Tick(Time.realtimeSinceStartupAsDouble);
            if (!canvasRoot.activeSelf) return;
            Animate(Time.unscaledTime);
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.lKey.wasPressedThisFrame && startButton.gameObject.activeSelf) { labRequested = true; OnStart(); return; }
            if ((kb.zKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) && startButton.gameObject.activeSelf) OnStart();
        }

        /// <summary>PRESS START 깜빡임(0.6초 켜짐·0.4초 꺼짐), 그림자 둘이 번갈아 1px 들썩임, 반짝이는 각자 다른 박자로 켜졌다 꺼짐</summary>
        void Animate(float t)
        {
            pressStart.enabled = startButton.gameObject.activeSelf && t % 1f < 0.6f;
            for (int i = 0; i < silhouettes.Length; i++)
            {
                var p = silhouettes[i].anchoredPosition;
                silhouettes[i].anchoredPosition = new Vector2(p.x, FeetY + (((int)(t * 2.2f) + i) % 2 == 0 ? 1 : 0));
            }
            for (int i = 0; i < sparkles.Length; i++) sparkles[i].enabled = ((int)(t * 2.5f + i * 0.7f)) % 3 != 0;
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
            statusBox.SetActive(false);
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
            ((RectTransform)demoButton.transform).anchoredPosition = new Vector2(s == LaunchState.TimedOut ? 50 : 0, -62);
            pressStart.text = "▶ " + locale.Get("title.press_start");
            footer.text = locale.Get("title.footer");
            foreach (var t in subtitle) t.text = locale.Get("title.subtitle");
            retryLabel.text = locale.Get("title.retry");
            demoLabel.text = locale.Get("title.try_demo");
            status.text = s switch
            {
                LaunchState.WaitingInit => locale.Get("title.connecting"),
                LaunchState.TimedOut => locale.Get("title.connect_failed"),
                LaunchState.Connected when noPartner => locale.Format("title.no_partner", string.Join(", ", battle.PlayableNameKeys().Select(locale.Get))),
                _ => "",
            };
            statusBox.SetActive(status.text.Length > 0);
        }

        // ------------------------------------------------------------------ 그림 (모두 코드로)
        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        static Texture2D NewTexture(int w, int h)
            => new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

        static Sprite ToSprite(Texture2D tex) => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f);

        /// <summary>하늘: 위 진한 하늘색 → 아래 흰색을 8px 계단으로 (퀴즈 하늘과 같은 색 지점 0 · 55 · 72 · 100%)</summary>
        static Sprite SkySprite()
        {
            const int bands = PixelCanvasScaler.BaseHeight / 8;
            var tex = NewTexture(1, bands);
            for (int i = 0; i < bands; i++)
            {
                float k = 1 - (i + 0.5f) / bands;   // 0 = 위, 1 = 아래 (텍스처 y=0 이 아래)
                var c = k < 0.55f ? Color.Lerp(SkyTop, SkyMid, k / 0.55f)
                      : k < 0.72f ? Color.Lerp(SkyMid, SkyLow, (k - 0.55f) / 0.17f)
                      : Color.Lerp(SkyLow, Color.white, (k - 0.72f) / 0.28f);
                tex.SetPixel(0, i, c);
            }
            tex.Apply();
            return ToSprite(tex);
        }

        /// <summary>아래쪽 뭉게구름: 흰 원을 겹쳐 그리고 바깥 가장자리에 옅은 하늘빛 테두리 1px</summary>
        static Sprite CloudSprite()
        {
            int w = PixelCanvasScaler.BaseWidth, h = CloudHeight;
            var puffs = new[] { (8, 30, 16), (34, 38, 18), (64, 28, 15), (92, 40, 20), (124, 30, 16), (150, 42, 19), (182, 31, 16), (210, 40, 19), (236, 30, 15) };
            bool Inside(int x, int y) => y < 18 || puffs.Any(p => (x - p.Item1) * (x - p.Item1) + (y - p.Item2 + 12) * (y - p.Item2 + 12) <= p.Item3 * p.Item3);
            var tex = NewTexture(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool on = Inside(x, y);
                    bool rim = on && (!Inside(x, y + 1) || !Inside(x - 1, y) || !Inside(x + 1, y));
                    tex.SetPixel(x, y, !on ? Color.clear : rim ? CloudRim : Color.white);
                }
            tex.Apply();
            return ToSprite(tex);
        }

        static Sprite plus;

        /// <summary>반짝이: 5×5 십자 (퀴즈의 노란 십자 반짝이)</summary>
        static Sprite PlusSprite()
        {
            if (plus != null) return plus;
            var tex = NewTexture(5, 5);
            for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++) tex.SetPixel(x, y, x == 2 || y == 2 ? Color.white : Color.clear);
            tex.Apply();
            return plus = ToSprite(tex);
        }

        /// <summary>
        /// 스프라이트의 모양(알파)만 흰색으로 옮긴 사본. 시트 텍스처는 읽기 불가(빌드 용량)라 RenderTexture 로 복사해 읽는다.
        /// Image.color 로 칠하면 색이 고른 그림자가 된다(원본 색에 곱하면 노란 피카츄가 얼룩진다).
        /// </summary>
        static Sprite SilhouetteOf(Sprite sprite)
        {
            var r = sprite.textureRect;
            int w = (int)r.width, h = (int)r.height;
            var rt = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(sprite.texture, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = NewTexture(w, h);
            tex.ReadPixels(new Rect(r.x, r.y, w, h), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(px[i].a > 127 ? 255 : 0));
            tex.SetPixels32(px);
            tex.Apply();
            return ToSprite(tex);
        }

        Image Picture(Transform parent, string name, Sprite sprite, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sprite.rect.size; rt.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = sprite; img.raycastTarget = false;
            return img;
        }

        RectTransform Silhouette(Transform parent, Sprite sprite, float x, bool flip)
        {
            var img = Picture(parent, "Silhouette", SilhouetteOf(sprite), Vector2.zero);
            img.color = Shadow;
            var rt = img.rectTransform;
            rt.pivot = new Vector2(0.5f, 0);   // 발 기준
            rt.anchoredPosition = new Vector2(x, FeetY);
            if (flip) rt.localScale = new Vector3(-1, 1, 1);
            return rt;
        }

        Image Sparkle(Transform parent, Vector2 pos, int index)
        {
            var img = Picture(parent, "Sparkle" + index, PlusSprite(), pos);
            img.color = Spark;
            return img;
        }

        TextMeshProUGUI Label(Transform parent, string name, Vector2 pos, float size)
        {
            var t = UiFactory.Text(parent, name, font, pos, new Vector2(236, size + 4), size);
            t.color = Ink; t.textWrappingMode = TextWrappingModes.NoWrap;
            return t;
        }

        /// <summary>
        /// 도트 테두리 글자: 흰 그림자(오른쪽 아래 shadowOff) → 테두리(linePx 만큼 8방향, 0 이면 왼쪽 위 1px) → 본문 순으로 겹친다.
        /// rich 는 본문에만 쓴다(색 태그 가능). 돌려주는 배열의 마지막이 본문이다.
        /// </summary>
        TextMeshProUGUI[] Outlined(Transform parent, string name, string rich, string plain, Vector2 pos, float size, Color fill, Color line, int linePx, int shadowOff)
        {
            var holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            ((RectTransform)holder.transform).anchoredPosition = pos;
            var copies = new List<TextMeshProUGUI>();
            void Copy(Vector2 off, Color c, string text)
            {
                var t = UiFactory.Text(holder.transform, "Copy", font, off, new Vector2(PixelCanvasScaler.BaseWidth, size + 8), size);
                t.textWrappingMode = TextWrappingModes.NoWrap; t.color = c; t.text = text;
                copies.Add(t);
            }
            Copy(new Vector2(shadowOff, -shadowOff), Color.white, plain);
            if (linePx == 0) Copy(new Vector2(-1, 1), line, plain);
            else
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (dx != 0 || dy != 0) Copy(new Vector2(dx, dy) * linePx, line, plain);
            Copy(Vector2.zero, fill, rich);
            return copies.ToArray();
        }

        GameObject Box(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(1, 1, 1, 0.92f);
            var o = go.GetComponent<Outline>(); o.effectColor = Ink; o.effectDistance = new Vector2(1, -1);
            return go;
        }

        Button StyledButton(Transform parent, string name, Vector2 pos, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI label)
        {
            var b = UiFactory.Button(parent, name, font, pos, new Vector2(90, 20), onClick, out label);
            b.GetComponent<Image>().color = LogoAccent;
            var o = b.gameObject.AddComponent<Outline>(); o.effectColor = LogoLine; o.effectDistance = new Vector2(1, -1);
            label.color = LogoLine;
            return b;
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
