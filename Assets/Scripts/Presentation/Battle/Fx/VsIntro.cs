using System.Collections;
using System.Collections.Generic;
using Pokepedia.Presentation.Audio;
using Pokepedia.Presentation.Audio.Synth;
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle.Fx
{
    /// <summary>
    /// 배틀 도입 VS 장면 (PROJECT.md 5.3, M5). HUD 위에 덮는 오버레이라 월드·후처리와 상관없이 그려진다.
    /// 화면을 대각선으로 갈라 위는 상대, 아래는 플레이어의 띠(타입 색)를 놓고, 두 포켓몬이 실루엣으로 들어온 뒤
    /// "VS"가 찍히는 순간 번쩍이며 색이 돌아온다. 확인 입력으로 건너뛸 수 있다. 끝나면 오버레이를 지운다.
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

        /// <summary>한쪽 정보: 앞모습 시트, 이름, 타입 색 2개(띠 바탕·속도선)</summary>
        public readonly struct Side
        {
            public readonly SpriteSheet Sheet; public readonly string Name; public readonly Color Main, Accent;
            public Side(SpriteSheet sheet, string name, Color main, Color accent) { Sheet = sheet; Name = name; Main = main; Accent = accent; }
        }

        /// <summary>대각선(갈라지는 선)의 y: 왼쪽 아래에서 오른쪽 위로 올라간다. 위쪽이 상대 띠, 아래쪽이 플레이어 띠.</summary>
        static float DividerY(float x) => 140 - x * 64f / W;

        public IEnumerator Play(Side player, Side npc)
        {
            var textures = new List<Object>();
            var root = Ui.Rect(hud, "VsIntro", 0, 0, W, H);
            root.SetAsLastSibling();
            vfx.BringToFront();   // 화면 번쩍임은 VS 장면 위에

            var black = Ui.Fill(root, "Black", 0, 0, W, H, Color.black);
            var npcBand = Band(root, "NpcBand", npc, top: true, textures);
            var playerBand = Band(root, "PlayerBand", player, top: false, textures);
            var divider = Band(root, "Divider", default, top: false, textures, divider: true);

            var lines = new List<(RectTransform rt, float speed, float y)>();
            for (int i = 0; i < 12; i++)
            {
                bool top = i % 2 == 0;
                var side = top ? npc : player;
                float y = top ? Random.Range(6, 70) : Random.Range(150, 208);
                var len = Random.Range(12, 44);
                var img = Ui.Fill(root, "Line" + i, Random.Range(0, W), y, len, 1, new Color(side.Accent.r, side.Accent.g, side.Accent.b, 0.75f));
                lines.Add((img.rectTransform, top ? -Random.Range(220, 360) : Random.Range(220, 360), y));
            }

            var (npcBox, npcSprite) = Pokemon(root, "NpcPokemon", npc.Sheet, flip: false);
            var (playerBox, playerSprite) = Pokemon(root, "PlayerPokemon", player.Sheet, flip: true);   // 앞모습은 왼쪽을 보므로 플레이어는 뒤집어 오른쪽(상대)을 보게
            var npcSize = Size(npc.Sheet); var playerSize = Size(player.Sheet);
            // 상대는 오른쪽 끝에 붙여(크면 오른쪽이 화면 밖으로 조금 잘린다) 가운데 VS 자리를 비운다
            var npcHome = new Vector2(Mathf.Max(W - 8 - npcSize.x, W - 128), Mathf.Max(2, DividerY(W - 64) - npcSize.y + 18));
            var playerHome = new Vector2(8, Mathf.Min(H - playerSize.y - 4, DividerY(8 + playerSize.x / 2) - 10));

            var npcName = NameLabel(root, "NpcName", npc.Name, 10, 12, TextAlignmentOptions.TopLeft);
            var playerName = NameLabel(root, "PlayerName", player.Name, W - 10 - 110, H - 26, TextAlignmentOptions.TopRight);

            // VS: 폰트 4배(48)로 도트를 유지하고, 사방 2px 짙은 테두리를 둘러 어떤 배경 위에서도 읽히게 한다
            var vs = Ui.Rect(root, "VS", W / 2 - 48, 80, 96, 56);
            var edge = new Color(0.08f, 0.06f, 0.14f);
            foreach (var o in new[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2), new Vector2(-2, -2), new Vector2(2, -2), new Vector2(-2, 2), new Vector2(2, 2), new Vector2(3, 4) })
            {
                var e = Ui.Label(vs, "Edge", font, o.x, o.y, 96, 56, TextAlignmentOptions.Center, 48);
                e.text = "VS"; e.color = edge;
            }
            var vsText = Ui.Label(vs, "Text", font, 0, 0, 96, 56, TextAlignmentOptions.Center, 48);
            vsText.text = "VS"; vsText.color = new Color(1f, 0.86f, 0.3f);
            vs.gameObject.SetActive(false);

            foreach (var img in new[] { npcSprite, playerSprite }) img.color = Color.black;   // 실루엣
            SetX(npcBand, W); SetX(playerBand, -W); divider.color = new Color(1, 1, 1, 0);
            Place(npcBox, npcHome + new Vector2(W, 0)); Place(playerBox, playerHome - new Vector2(W, 0));
            npcName.SetActive(false); playerName.SetActive(false);

            sound.Play(SfxKind.SendOut);
            bool skipped = false;
            float time = 0;

            // 진행: (시작, 끝, 동작) — 매 프레임 지금 시각까지의 상태를 계산한다
            const float BandIn = 0.28f, MonIn = 0.5f, Slam = 0.62f, Hold = 1.75f, Out = 2.05f;
            bool slammed = false;
            while (time < Out)
            {
                if (time > 0.3f && !skipped && BattleInput.Advance) { skipped = true; time = Mathf.Max(time, Hold); }
                float bandK = Ease(Mathf.Clamp01(time / BandIn));
                SetX(npcBand, Mathf.Round(W * (1 - bandK))); SetX(playerBand, Mathf.Round(-W * (1 - bandK)));
                divider.color = new Color(1, 1, 1, bandK);

                float monK = Ease(Mathf.Clamp01((time - 0.18f) / (MonIn - 0.18f)));
                Place(npcBox, npcHome + new Vector2(Mathf.Round(90 * (1 - monK)), 0));
                Place(playerBox, playerHome - new Vector2(Mathf.Round(90 * (1 - monK)), 0));
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
                    }
                }
                if (slammed)
                {
                    float k = Mathf.Clamp01((time - Slam) / 0.12f);
                    vs.localScale = Vector3.one * (k < 1 ? Mathf.Lerp(3f, 1f, Ease(k)) : 1f);
                }

                foreach (var (rt, speed, y) in lines)
                {
                    float x = rt.anchoredPosition.x + speed * Time.deltaTime;
                    if (speed < 0 && x < -50) x += W + 60;
                    if (speed > 0 && x > W + 10) x -= W + 60;
                    rt.anchoredPosition = new Vector2(Mathf.Round(x), -y);
                }

                // 나가기: 띠는 들어온 반대쪽으로, 검은 바탕은 옅어지며 전투 화면이 드러난다
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
            foreach (var t in textures) Object.Destroy(t);
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

        // ------------------------------------------------------------------ 띠 그림 (코드로 그린 도트)
        static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static float Th(int x, int y) => (Bayer[(y & 3) * 4 + (x & 3)] + 0.5f) / 16f;

        static Image Band(RectTransform root, string name, Side side, bool top, List<Object> owned, bool divider = false)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            var dark = side.Main * 0.32f; dark.a = 1;
            var mid = side.Main * 0.7f; mid.a = 1;
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = y - DividerY(x);   // 음수 = 선 위
                Color c = Color.clear;
                if (divider)
                {
                    if (Mathf.Abs(d) <= 1.5f) c = Color.white;
                }
                else if (top ? d < -1.5f : d > 1.5f)
                {
                    // 바깥(화면 끝)은 어둡고 대각선 쪽은 밝다 — 디더링으로 3단계를 섞는다
                    float v = Mathf.Clamp01(1 - Mathf.Abs(d) / 110f) * 2f;
                    c = v < 1 ? (v > Th(x, y) ? mid : dark) : (v - 1 > Th(x, y) ? side.Main : mid);
                    c.a = 1;
                }
                px[(H - 1 - y) * W + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0, 1), 1);
            owned.Add(tex); owned.Add(sprite);
            var rt = Ui.Rect(root, name, 0, 0, W, H);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }
    }
}
