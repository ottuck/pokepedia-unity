using System;
using System.Collections;
using System.Collections.Generic;
using Pokepedia.Presentation.Audio;
using Pokepedia.Presentation.Audio.Synth;
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Pokepedia.Presentation.Battle.Fx
{
    /// <summary>
    /// 배틀 도입 VS 장면 (PROJECT.md 5.3·5.5, M5). HUD 위에 덮는 오버레이라 월드·후처리와 상관없이 그려진다.
    /// 공통 흐름: 배경이 들어오고 → 두 포켓몬이 실루엣으로 자리를 잡고 → "VS"가 찍히는 순간 번쩍이며 색이 돌아오고 → 옅어지며 전투 화면.
    /// 확인 입력으로 건너뛴다. 배경 구성과 배치는 스타일(A 대각선 띠 / B 가로 띠 + 폭발 문장 / C 스포트라이트 + 번개 균열)마다 다르다.
    /// 모든 그림은 코드로 그린 도트이고 크기는 정수 배율로만 바꾼다.
    /// </summary>
    public sealed class VsIntro
    {
        public enum Style { DiagonalBands, HorizontalBars, Spotlight }

        /// <summary>실제 배틀에서 쓰는 스타일. 사용자가 실험실에서 고르면 바꾼다.</summary>
        public static Style Default = Style.DiagonalBands;

        public static string Label(Style s) => s switch { Style.DiagonalBands => "VS A", Style.HorizontalBars => "VS B", _ => "VS C" };

        const int W = PixelCanvasScaler.BaseWidth, H = PixelCanvasScaler.BaseHeight;
        const int SpriteScale = 2;   // 정수 배율이라 도트가 고르게 유지된다

        readonly MonoBehaviour host;
        readonly RectTransform hud;
        readonly TMP_FontAsset font;
        readonly SoundBank sound;
        readonly VfxLayer vfx;

        public VsIntro(MonoBehaviour host, RectTransform hud, TMP_FontAsset font, SoundBank sound, VfxLayer vfx)
        {
            this.host = host; this.hud = hud; this.font = font; this.sound = sound; this.vfx = vfx;
        }

        /// <summary>한쪽 정보: 앞모습 시트, 이름, 타입 색 2개(바탕·강조)</summary>
        public readonly struct Side
        {
            public readonly SpriteSheet Sheet; public readonly string Name; public readonly Color Main, Accent;
            public Side(SpriteSheet sheet, string name, Color main, Color accent) { Sheet = sheet; Name = name; Main = main; Accent = accent; }
        }

        /// <summary>스타일마다 다른 부분: 배치, 매 프레임 배경 움직임, VS 가 찍힐 때의 추가 효과</summary>
        sealed class Scene
        {
            public Vector2 NpcHome, PlayerHome, NpcFrom, PlayerFrom;   // From = 들어오기 전 위치까지의 차이
            public bool RiseIn;                                          // true 면 옆이 아니라 아래에서 떠오르며 옅게 → 진하게
            public Vector2 NpcName, PlayerName;
            public Vector2 VsCenter = new Vector2(W / 2f, 108);              // 포켓몬에 가리지 않는 자리
            public Action<float> Update;                                 // 경과 시간
            public Action OnSlam;
        }

        const float BgIn = 0.28f, MonStart = 0.18f, MonIn = 0.5f, Slam = 0.62f, Hold = 1.75f, Out = 2.05f;

        public IEnumerator Play(Side player, Side npc) => Play(player, npc, Default);

        public IEnumerator Play(Side player, Side npc, Style style)
        {
            var owned = new List<Object>();
            var root = Ui.Rect(hud, "VsIntro", 0, 0, W, H);
            root.SetAsLastSibling();
            vfx.BringToFront();   // 화면 번쩍임은 VS 장면 위에
            var black = Ui.Fill(root, "Black", 0, 0, W, H, Color.black);

            var npcSize = Size(npc.Sheet); var playerSize = Size(player.Sheet);
            var scene = style switch
            {
                Style.HorizontalBars => Bars(root, player, npc, npcSize, playerSize, owned),
                Style.Spotlight => Spotlight(root, player, npc, npcSize, playerSize, owned),
                _ => Diagonal(root, player, npc, npcSize, playerSize, owned),
            };

            var (npcBox, npcSprite) = Pokemon(root, "NpcPokemon", npc.Sheet, flip: false);
            var (playerBox, playerSprite) = Pokemon(root, "PlayerPokemon", player.Sheet, flip: true);   // 앞모습은 왼쪽을 보므로 플레이어는 뒤집어 상대를 보게
            var npcName = NameLabel(root, "NpcName", npc.Name, scene.NpcName.x, scene.NpcName.y, TextAlignmentOptions.TopLeft);
            var playerName = NameLabel(root, "PlayerName", player.Name, scene.PlayerName.x, scene.PlayerName.y, TextAlignmentOptions.TopRight);
            var vs = VsLabel(root, scene.VsCenter);
            vs.gameObject.SetActive(false);

            foreach (var img in new[] { npcSprite, playerSprite }) img.color = scene.RiseIn ? new Color(0, 0, 0, 0) : Color.black;   // 실루엣
            npcName.SetActive(false); playerName.SetActive(false);

            sound.Play(SfxKind.SendOut);
            bool skipped = false, slammed = false;
            float time = 0;
            while (time < Out)
            {
                if (time > 0.3f && !skipped && BattleInput.Advance) { skipped = true; time = Mathf.Max(time, Hold); }
                scene.Update(time);

                float monK = Ease(Mathf.Clamp01((time - MonStart) / (MonIn - MonStart)));
                Place(npcBox, scene.NpcHome + scene.NpcFrom * (1 - monK));
                Place(playerBox, scene.PlayerHome + scene.PlayerFrom * (1 - monK));
                if (scene.RiseIn && !slammed)
                    foreach (var img in new[] { npcSprite, playerSprite }) img.color = new Color(0, 0, 0, monK);
                bool namesOn = time > MonIn - 0.05f;
                npcName.SetActive(namesOn); playerName.SetActive(namesOn);

                if (!slammed && time >= Slam)
                {
                    slammed = true;
                    vs.gameObject.SetActive(true);
                    npcSprite.color = playerSprite.color = Color.white;   // 실루엣 → 색
                    if (!skipped)   // 건너뛰면 소리·번쩍임 없이 바로 나간다
                    {
                        sound.Play(SfxKind.CriticalHit);
                        host.StartCoroutine(vfx.ScreenFlash(new Color(1, 1, 1, 0.9f), 0.3f));
                        host.StartCoroutine(vfx.Shake(3, 0.3f));
                        scene.OnSlam?.Invoke();
                    }
                }
                if (slammed)
                {
                    float k = Mathf.Clamp01((time - Slam) / 0.12f);
                    vs.localScale = Vector3.one * (k < 1 ? Mathf.Lerp(3f, 1f, Ease(k)) : 1f);
                }

                // 나가기: 검은 바탕과 모든 그림이 옅어지며 전투 화면이 드러난다
                if (time > Hold)
                {
                    float outK = Ease(Mathf.Clamp01((time - Hold) / (Out - Hold)));
                    black.color = new Color(0, 0, 0, 1 - outK);
                    foreach (var g in root.GetComponentsInChildren<Graphic>())
                        if (g != black) { var c = g.color; c.a = Mathf.Min(c.a, 1 - outK); g.color = c; }
                }

                time += Time.deltaTime;
                yield return null;
            }

            Object.Destroy(root.gameObject);
            foreach (var t in owned) Object.Destroy(t);
        }

        // ================================================================== A: 대각선 띠
        /// <summary>대각선(갈라지는 선)의 y: 왼쪽 아래에서 오른쪽 위로 올라간다. 위쪽이 상대 띠, 아래쪽이 플레이어 띠.</summary>
        static float DividerY(float x) => 140 - x * 64f / W;

        Scene Diagonal(RectTransform root, Side player, Side npc, Vector2 npcSize, Vector2 playerSize, List<Object> owned)
        {
            var npcBand = MakeImage(root, "NpcBand", Canvas(W, H, (x, y) => DiagonalBand(x, y, npc, top: true)), owned);
            var playerBand = MakeImage(root, "PlayerBand", Canvas(W, H, (x, y) => DiagonalBand(x, y, player, top: false)), owned);
            var divider = MakeImage(root, "Divider", Canvas(W, H, (x, y) => Mathf.Abs(y - DividerY(x)) <= 1.5f ? Color.white : Color.clear), owned);
            var lines = SpeedLines(root, npc, player, (6, 70), (150, 208));
            return new Scene
            {
                // 상대는 오른쪽 끝에 붙여(크면 오른쪽이 화면 밖으로 조금 잘린다) 가운데 VS 자리를 비운다
                NpcHome = new Vector2(Mathf.Max(W - 8 - npcSize.x, W - 128), Mathf.Max(2, DividerY(W - 64) - npcSize.y + 18)),
                PlayerHome = new Vector2(8, Mathf.Min(H - playerSize.y - 4, DividerY(8 + playerSize.x / 2) - 10)),
                NpcFrom = new Vector2(90, 0), PlayerFrom = new Vector2(-90, 0),
                NpcName = new Vector2(10, 12), PlayerName = new Vector2(W - 10 - 110, H - 26),
                Update = t =>
                {
                    float k = Ease(Mathf.Clamp01(t / BgIn));
                    SetX(npcBand, Mathf.Round(W * (1 - k))); SetX(playerBand, Mathf.Round(-W * (1 - k)));
                    divider.color = new Color(1, 1, 1, k);
                    lines(Time.deltaTime);
                },
            };
        }

        static Color DiagonalBand(int x, int y, Side side, bool top)
        {
            float d = y - DividerY(x);   // 음수 = 선 위
            if (top ? d >= -1.5f : d <= 1.5f) return Color.clear;
            // 바깥(화면 끝)은 어둡고 대각선 쪽은 밝다 — 디더링으로 3단계를 섞는다
            return Ramp3(side.Main, Mathf.Clamp01(1 - Mathf.Abs(d) / 110f) * 2f, x, y);
        }

        // ================================================================== B: 가로 띠 + 폭발 문장
        const int BarTop0 = 14, BarTop1 = 90, BarBot0 = 126, BarBot1 = 202;

        Scene Bars(RectTransform root, Side player, Side npc, Vector2 npcSize, Vector2 playerSize, List<Object> owned)
        {
            var npcBar = MakeImage(root, "NpcBar", Canvas(W, H, (x, y) => Bar(x, y, npc, BarTop0, BarTop1, fromRight: true)), owned);
            var playerBar = MakeImage(root, "PlayerBar", Canvas(W, H, (x, y) => Bar(x, y, player, BarBot0, BarBot1, fromRight: false)), owned);
            var lines = SpeedLines(root, npc, player, (2, 120), (96, 214), count: 18, white: true);

            // 폭발 문장: 들쭉날쭉한 별(12가시) — 빨강·주황 두 겹 + 짙은 테두리. VS 가 찍힐 때 2배 → 1배로 "쾅"
            const int N = 61;
            var burst = MakeImage(root, "Burst", Canvas(N, N, (x, y) => Burst(x, y, N)), owned, W / 2 - N / 2, H / 2 - N / 2 - 2, N, N);
            burst.rectTransform.pivot = new Vector2(0.5f, 0.5f);   // 가운데 기준으로 커지게
            burst.rectTransform.anchoredPosition = new Vector2(96, -108);
            burst.gameObject.SetActive(false);
            float slamAt = -1;
            return new Scene
            {
                NpcHome = new Vector2(Mathf.Max(W - 6 - npcSize.x, W - 104), Mathf.Max(0, BarTop1 + 12 - npcSize.y)),   // 띠 아래로 살짝 튀어나오고, 크면 오른쪽이 잘린다
                PlayerHome = new Vector2(6, Mathf.Min(H - playerSize.y, BarBot1 - playerSize.y + 4)),
                NpcFrom = new Vector2(110, 0), PlayerFrom = new Vector2(-110, 0),
                NpcName = new Vector2(10, BarTop1 - 20), PlayerName = new Vector2(W - 10 - 110, BarBot0 + 4),
                VsCenter = new Vector2(96, 108),
                Update = t =>
                {
                    float k = Ease(Mathf.Clamp01(t / BgIn));
                    SetX(npcBar, Mathf.Round(W * (1 - k))); SetX(playerBar, Mathf.Round(-W * (1 - k)));
                    lines(Time.deltaTime);
                    if (slamAt >= 0)
                    {
                        float s = t - slamAt < 0.08f ? 2 : 1;
                        burst.rectTransform.localScale = new Vector3(s, s, 1);
                    }
                },
                OnSlam = () => { burst.gameObject.SetActive(true); slamAt = Slam; burst.transform.SetSiblingIndex(burst.transform.parent.childCount - 2); },
            };
        }

        static Color Bar(int x, int y, Side side, int y0, int y1, bool fromRight)
        {
            if (y < y0 || y > y1) return Color.clear;
            if (y == y0 || y == y1) return side.Accent;                       // 위·아래 1px 밝은 테두리
            if (y == y0 + 1 || y == y1 - 1) return Color.black;
            // 들어오는 쪽(화면 끝)이 밝고 반대쪽으로 어두워지는 가로 그라디언트
            float v = (fromRight ? x : W - 1 - x) / (float)W * 2f;
            return Ramp3(side.Main, v, x, y);
        }

        static Color Burst(int x, int y, int n)
        {
            float c = (n - 1) / 2f, dx = x - c, dy = y - c;
            float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
            // 12가시, 가시마다 길이를 조금씩 달리해 손으로 그린 듯 들쭉날쭉하게
            float spike = Mathf.Abs(Mathf.Sin(a * 6)), jag = 0.85f + 0.15f * Mathf.Sin(a * 17 + 1.3f);
            float edge = (17 + 13 * spike) * jag;
            if (r > edge + 2) return Color.clear;
            if (r > edge) return new Color(0.1f, 0.04f, 0.08f);
            if (r > edge * 0.62f) return new Color(0.9f, 0.18f, 0.12f);
            return new Color(1f, 0.6f, 0.15f);
        }

        // ================================================================== C: 스포트라이트 + 번개 균열
        Scene Spotlight(RectTransform root, Side player, Side npc, Vector2 npcSize, Vector2 playerSize, List<Object> owned)
        {
            var npcHome = new Vector2(Mathf.Max(W - 10 - npcSize.x, W - 104), 6);
            var playerHome = new Vector2(10, H - playerSize.y - 22);
            var npcLight = Glow(root, "NpcLight", npc, npcHome + npcSize / 2, owned);
            var playerLight = Glow(root, "PlayerLight", player, playerHome + playerSize / 2, owned);
            var crack = MakeImage(root, "Crack", Canvas(W, H, Crack), owned);
            crack.color = new Color(1, 1, 1, 0);
            var ringSprites = new[] { RingSprite(18, owned), RingSprite(36, owned), RingSprite(60, owned), RingSprite(90, owned) };
            float slamAt = -1;
            var rings = new List<Image>();
            return new Scene
            {
                NpcHome = npcHome, PlayerHome = playerHome,
                NpcFrom = new Vector2(0, 24), PlayerFrom = new Vector2(0, 24), RiseIn = true,
                NpcName = new Vector2(10, 14), PlayerName = new Vector2(W - 10 - 110, H - 24),
                VsCenter = new Vector2(112, 112),
                Update = t =>
                {
                    float k = Ease(Mathf.Clamp01(t / 0.4f));
                    npcLight.color = playerLight.color = new Color(1, 1, 1, k);
                    if (slamAt < 0) return;
                    float s = t - slamAt;
                    // 균열: 찍히는 순간 하얗게 번쩍이고 잠깐 뒤 옅게 남는다
                    crack.color = new Color(1, 1, 1, s < 0.1f ? 1 : Mathf.Lerp(1, 0.55f, Mathf.Clamp01((s - 0.1f) / 0.3f)));
                    // 충격파 고리 2개: 반지름이 다른 얇은 고리 그림을 차례로 바꿔 퍼져 나가게(크기를 키우면 선이 두꺼워진다)
                    for (int i = 0; i < rings.Count; i++)
                    {
                        float rk = (s - i * 0.12f) / 0.4f;
                        if (rk < 0 || rk >= 1) { rings[i].color = new Color(1, 1, 1, 0); continue; }
                        var sp = ringSprites[Mathf.Min(ringSprites.Length - 1, (int)(rk * ringSprites.Length))];
                        if (rings[i].sprite != sp)
                        {
                            rings[i].sprite = sp;
                            rings[i].rectTransform.sizeDelta = new Vector2(sp.rect.width, sp.rect.height);
                        }
                        rings[i].color = new Color(1, 1, 1, 1 - rk * 0.8f);
                    }
                },
                OnSlam = () =>
                {
                    slamAt = Slam;
                    for (int i = 0; i < 2; i++)
                    {
                        var r = MakeImage(root, "Shockwave" + i, ringSprites[0], 0, 0, 37, 37);
                        r.color = new Color(1, 1, 1, 0);
                        r.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        r.rectTransform.anchoredPosition = new Vector2(112, -112);
                        rings.Add(r);
                    }
                },
            };
        }

        Image Glow(RectTransform root, string name, Side side, Vector2 center, List<Object> owned)
        {
            const int N = 150;
            var img = MakeImage(root, name, Canvas(N, N, (x, y) =>
            {
                float c = (N - 1) / 2f, d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                if (d > 1) return Color.clear;
                var col = Ramp3(side.Main, (1 - d) * 2.2f, x, y);
                return (1 - d) > Th(x, y) * 0.9f ? col : Color.clear;   // 바깥으로 갈수록 디더링으로 성글어진다
            }), owned, Mathf.Round(center.x - N / 2f), Mathf.Round(center.y - N / 2f), N, N);
            img.color = new Color(1, 1, 1, 0);
            return img;
        }

        /// <summary>오른쪽 위에서 왼쪽 아래로 화면을 가르는 지그재그 균열(흰 3px + 양옆 연한 빛)</summary>
        static Color Crack(int x, int y)
        {
            // 기준선: (W+10, -10) → (-10, H+10). 일정 간격마다 좌우로 꺾인다
            float t = (x - (W + 10)) / -(W + 20f);
            float baseY = -10 + t * (H + 20);
            float zig = ((int)(t * 9) % 2 == 0 ? 1 : -1) * (Mathf.Repeat(t * 9, 1) - 0.5f) * 22;
            float d = Mathf.Abs(y - (baseY + zig));
            if (d <= 1.5f) return Color.white;
            if (d <= 3.5f) return new Color(1f, 0.95f, 0.6f, 0.8f);
            return Color.clear;
        }

        /// <summary>반지름 r 의 1.5px 고리(지름 2r+1)</summary>
        static Sprite RingSprite(int r, List<Object> owned)
        {
            int n = 2 * r + 1;
            return MakeSprite(n, n, (x, y) =>
            {
                float d = Mathf.Sqrt((x - r) * (x - r) + (y - r) * (y - r));
                return d <= r + 0.5f && d >= r - 1f ? Color.white : Color.clear;
            }, owned);
        }

        // ================================================================== 공통 부품
        /// <summary>띠 위를 흐르는 속도선. 위쪽 범위는 왼쪽으로, 아래쪽 범위는 오른쪽으로. 매 프레임 dt 를 넘겨 움직인다.</summary>
        Action<float> SpeedLines(RectTransform root, Side npc, Side player, (int, int) topRange, (int, int) bottomRange, int count = 12, bool white = false)
        {
            var lines = new List<(RectTransform rt, float speed, float y)>();
            for (int i = 0; i < count; i++)
            {
                bool top = i % 2 == 0;
                var side = top ? npc : player;
                float y = top ? Random.Range(topRange.Item1, topRange.Item2) : Random.Range(bottomRange.Item1, bottomRange.Item2);
                var color = white ? new Color(1, 1, 1, 0.35f) : new Color(side.Accent.r, side.Accent.g, side.Accent.b, 0.75f);
                var img = Ui.Fill(root, "Line" + i, Random.Range(0, W), y, Random.Range(12, 44), 1, color);
                lines.Add((img.rectTransform, top ? -Random.Range(220, 360) : Random.Range(220, 360), y));
            }
            return dt =>
            {
                foreach (var (rt, speed, y) in lines)
                {
                    float x = rt.anchoredPosition.x + speed * dt;
                    if (speed < 0 && x < -50) x += W + 60;
                    if (speed > 0 && x > W + 10) x -= W + 60;
                    rt.anchoredPosition = new Vector2(Mathf.Round(x), -y);
                }
            };
        }

        /// <summary>VS: 폰트 4배(48)로 도트를 유지하고, 사방 2px 짙은 테두리를 둘러 어떤 배경 위에서도 읽히게 한다</summary>
        RectTransform VsLabel(RectTransform root, Vector2 center)
        {
            var vs = Ui.Rect(root, "VS", 0, 0, 96, 56);
            vs.pivot = new Vector2(0.5f, 0.5f);
            vs.anchoredPosition = new Vector2(Mathf.Round(center.x), -Mathf.Round(center.y));
            var edge = new Color(0.08f, 0.06f, 0.14f);
            foreach (var o in new[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2), new Vector2(-2, -2), new Vector2(2, -2), new Vector2(-2, 2), new Vector2(2, 2), new Vector2(3, 4) })
            {
                var e = Ui.Label(vs, "Edge", font, o.x, o.y, 96, 56, TextAlignmentOptions.Center, 48);
                e.text = "VS"; e.color = edge;
            }
            var text = Ui.Label(vs, "Text", font, 0, 0, 96, 56, TextAlignmentOptions.Center, 48);
            text.text = "VS"; text.color = new Color(1f, 0.86f, 0.3f);
            return vs;
        }

        static float Ease(float k) => 1 - (1 - k) * (1 - k);

        static Vector2 Size(SpriteSheet sheet) => sheet != null ? (Vector2)sheet.Size * SpriteScale : new Vector2(96, 96);

        static void SetX(Image img, float x) => img.rectTransform.anchoredPosition = new Vector2(x, 0);

        static void Place(RectTransform box, Vector2 topLeft) => box.anchoredPosition = new Vector2(Mathf.Round(topLeft.x), -Mathf.Round(topLeft.y));

        /// <summary>포켓몬 그림: 왼쪽 위 기준 상자 안에 꽉 찬 Image. 뒤집을 때는 안쪽 Image 만 가운데 기준으로 뒤집어 상자 위치가 그대로다.</summary>
        static (RectTransform, Image) Pokemon(RectTransform root, string name, SpriteSheet sheet, bool flip)
        {
            var size = Size(sheet);
            var box = Ui.Rect(root, name, 0, 0, size.x, size.y);
            var go = new GameObject("Sprite", typeof(RectTransform));
            go.transform.SetParent(box, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (flip) rt.localScale = new Vector3(-1, 1, 1);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            if (sheet != null && sheet.Frames.Length > 0) img.sprite = sheet.Frames[0];
            return (box, img);
        }

        /// <summary>이름표: 1px 그림자 + 흰 글자. 둘을 한 상자에 넣어 함께 켜고 끈다(그림자를 먼저 넣어 글자 아래에 그려지게).</summary>
        GameObject NameLabel(RectTransform root, string name, string text, float x, float y, TextAlignmentOptions align)
        {
            var box = Ui.Rect(root, name, x, y, 112, 18);
            var shadow = Ui.Label(box, "Shadow", font, 1, 1, 110, 16, align);
            shadow.text = text; shadow.color = new Color(0, 0, 0, 0.8f);
            var label = Ui.Label(box, "Text", font, 0, 0, 110, 16, align);
            label.text = text; label.color = Color.white;
            return box.gameObject;
        }

        // ------------------------------------------------------------------ 도트 그리기
        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static float Th(int x, int y) => (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;

        /// <summary>v(0~2)를 어둠 → 중간 → 본색 3단계로, 단계 사이는 디더링으로 섞는다</summary>
        static Color Ramp3(Color main, float v, int x, int y)
        {
            var dark = main * 0.32f; dark.a = 1;
            var mid = main * 0.7f; mid.a = 1;
            var full = main; full.a = 1;
            return v < 1 ? (v > Th(x, y) ? mid : dark) : (v - 1 > Th(x, y) ? full : mid);
        }

        /// <summary>w×h 그림(왼쪽 위가 (0,0))을 스프라이트로. 텍스처·스프라이트는 owned 에 넣어 끝나면 지운다.</summary>
        static Sprite MakeSprite(int w, int h, Func<int, int, Color> pixel, List<Object> owned)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = pixel(x, y);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0, 1), 1);
            owned.Add(tex); owned.Add(sprite);
            return sprite;
        }

        /// <summary>코드로 그릴 그림: 크기와 픽셀 함수</summary>
        readonly struct Pic
        {
            public readonly int W, H; public readonly Func<int, int, Color> Px;
            public Pic(int w, int h, Func<int, int, Color> px) { W = w; H = h; Px = px; }
        }

        static Pic Canvas(int w, int h, Func<int, int, Color> pixel) => new Pic(w, h, pixel);

        /// <summary>그림을 스프라이트로 만들어 (x, y)에 w×h 로 놓는다. 크기를 안 주면 그림 크기 그대로.</summary>
        static Image MakeImage(RectTransform root, string name, Pic pic, List<Object> owned, float x = 0, float y = 0, float w = -1, float h = -1)
            => MakeImage(root, name, MakeSprite(pic.W, pic.H, pic.Px, owned), x, y, w < 0 ? pic.W : w, h < 0 ? pic.H : h);

        static Image MakeImage(RectTransform root, string name, Sprite sprite, float x, float y, float w, float h)
        {
            var rt = Ui.Rect(root, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }
    }
}
