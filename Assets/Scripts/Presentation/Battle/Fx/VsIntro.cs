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
    /// 배틀 도입 VS 장면의 에디터용 대체(PROJECT.md 5.3·5.5). 웹 빌드는 HTML 이 VS 를 그리고(BattleEventPlayer.PlayHtmlVs), 이것은 HTML 이 없는 에디터에서만 쓴다.
    /// HUD 위에 덮는 오버레이라 월드·후처리와 상관없이 그려진다.
    /// 흐름: 배경이 들어오고 → 두 포켓몬이 실루엣으로 자리를 잡고 → "VS"가 찍히는 순간 번쩍이며 색이 돌아오고 → 옅어지며 전투 화면.
    /// 확인 입력으로 건너뛴다. 배경은 좌우 대각선(위 60% → 아래 40%)으로 갈린 타입 색 띠다: 왼쪽 플레이어 · 오른쪽 상대.
    /// 모든 그림은 코드로 그린 도트이고 크기는 정수 배율로만 바꾼다.
    /// </summary>
    public sealed class VsIntro
    {
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

        /// <summary>배경이 정하는 부분: 배치, 매 프레임 배경 움직임, VS 가 찍힐 때의 추가 효과</summary>
        sealed class Scene
        {
            public Vector2 NpcHome, PlayerHome, NpcFrom, PlayerFrom;   // From = 들어오기 전 위치까지의 차이
            public Vector2 NpcName, PlayerName;
            public Vector2 VsCenter = new Vector2(W / 2f, 108);              // 포켓몬에 가리지 않는 자리
            public Action<float> Update;                                 // 경과 시간
            public Action OnSlam;
        }

        const float BgIn = 0.28f, MonStart = 0.18f, MonIn = 0.5f, Slam = 0.62f, Hold = 1.75f, Out = 2.05f;

        public IEnumerator Play(Side player, Side npc)
        {
            var owned = new List<Object>();
            var root = Ui.Rect(hud, "VsIntro", 0, 0, W, H);
            root.SetAsLastSibling();
            vfx.BringToFront();   // 화면 번쩍임은 VS 장면 위에
            var black = Ui.Fill(root, "Black", 0, 0, W, H, Color.black);

            var npcSize = Size(npc.Sheet); var playerSize = Size(player.Sheet);
            var scene = Diagonal(root, player, npc, npcSize, playerSize, owned);

            var (npcBox, npcSprite) = Pokemon(root, "NpcPokemon", npc.Sheet, flip: false);
            var (playerBox, playerSprite) = Pokemon(root, "PlayerPokemon", player.Sheet, flip: true);   // 앞모습은 왼쪽을 보므로 플레이어는 뒤집어 상대를 보게
            var npcName = NameLabel(root, "NpcName", npc.Name, scene.NpcName.x, scene.NpcName.y, TextAlignmentOptions.TopRight);
            var playerName = NameLabel(root, "PlayerName", player.Name, scene.PlayerName.x, scene.PlayerName.y, TextAlignmentOptions.TopLeft);
            var vs = VsLabel(root, scene.VsCenter);
            vs.gameObject.SetActive(false);

            foreach (var img in new[] { npcSprite, playerSprite }) img.color = Color.black;   // 실루엣
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

        // ================================================================== 대각선 띠
        /// <summary>대각선(갈라지는 선)의 x: 위 60% 에서 아래 40% 로 내려온다. 왼쪽이 플레이어 띠, 오른쪽이 상대 띠.</summary>
        static float DividerX(float y) => W * 0.6f - y * (W * 0.2f) / H;

        Scene Diagonal(RectTransform root, Side player, Side npc, Vector2 npcSize, Vector2 playerSize, List<Object> owned)
        {
            var npcBand = MakeImage(root, "NpcBand", Canvas(W, H, (x, y) => DiagonalBand(x, y, npc, right: true)), owned);
            var playerBand = MakeImage(root, "PlayerBand", Canvas(W, H, (x, y) => DiagonalBand(x, y, player, right: false)), owned);
            var divider = MakeImage(root, "Divider", Canvas(W, H, (x, y) => Mathf.Abs(x - DividerX(y)) <= 1.5f ? Color.white : Color.clear), owned);
            var lines = SpeedLines(root, npc, player, (8, 206), (8, 206), DividerX);
            return new Scene
            {
                // 상대는 오른쪽 위(크면 오른쪽이 화면 밖으로 조금 잘린다), 플레이어는 왼쪽 아래 — 가운데 대각선의 VS 자리를 비운다
                NpcHome = new Vector2(Mathf.Max(W - 8 - npcSize.x, W - 128), 26),
                PlayerHome = new Vector2(8, H - playerSize.y - 4),
                NpcFrom = new Vector2(90, 0), PlayerFrom = new Vector2(-90, 0),
                NpcName = new Vector2(W - 10 - 110, 8), PlayerName = new Vector2(10, H - 26),
                Update = t =>
                {
                    float k = Ease(Mathf.Clamp01(t / BgIn));
                    SetX(npcBand, Mathf.Round(W * (1 - k))); SetX(playerBand, Mathf.Round(-W * (1 - k)));
                    divider.color = new Color(1, 1, 1, k);
                    lines(Time.deltaTime);
                },
            };
        }

        static Color DiagonalBand(int x, int y, Side side, bool right)
        {
            float d = x - DividerX(y);   // 음수 = 선 왼쪽
            if (right ? d <= 1.5f : d >= -1.5f) return Color.clear;
            // 바깥(화면 끝)은 어둡고 대각선 쪽은 밝다 — 디더링으로 3단계를 섞는다
            return Ramp3(side.Main, Mathf.Clamp01(1 - Mathf.Abs(d) / 120f) * 2f, x, y);
        }

        // ================================================================== 공통 부품
        /// <summary>
        /// 띠 위를 흐르는 속도선: 상대 쪽(오른쪽)은 왼쪽으로, 플레이어 쪽(왼쪽)은 오른쪽으로 — 양쪽에서 대각선을 향해 몰려든다.
        /// dividerX 를 주면 선은 자기 쪽 띠 안에서만 보인다. 매 프레임 dt 를 넘겨 움직인다.
        /// </summary>
        Action<float> SpeedLines(RectTransform root, Side npc, Side player, (int, int) topRange, (int, int) bottomRange, Func<float, float> dividerX = null, int count = 14)
        {
            var lines = new List<(RectTransform rt, float speed, float y)>();
            var images = new List<Image>();
            for (int i = 0; i < count; i++)
            {
                bool top = i % 2 == 0;
                var side = top ? npc : player;
                float y = top ? Random.Range(topRange.Item1, topRange.Item2) : Random.Range(bottomRange.Item1, bottomRange.Item2);
                var color = new Color(side.Accent.r, side.Accent.g, side.Accent.b, 0.75f);
                var img = Ui.Fill(root, "Line" + i, Random.Range(0, W), y, Random.Range(12, 44), 1, color);
                lines.Add((img.rectTransform, top ? -Random.Range(220, 360) : Random.Range(220, 360), y));
                images.Add(img);
            }
            return dt =>
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var (rt, speed, y) = lines[i];
                    float x = rt.anchoredPosition.x + speed * dt;
                    if (speed < 0 && x < -50) x += W + 60;
                    if (speed > 0 && x > W + 10) x -= W + 60;
                    rt.anchoredPosition = new Vector2(Mathf.Round(x), -y);
                    if (dividerX != null)
                    {
                        float edge = dividerX(y);
                        images[i].enabled = speed < 0 ? x >= edge + 2 : x + rt.sizeDelta.x <= edge - 2;
                    }
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
