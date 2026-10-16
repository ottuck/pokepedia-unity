using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.UI
{
    /// <summary>
    /// 배틀 화면용 uGUI 생성 도우미. 좌표는 캔버스 픽셀(240×216), 원점은 부모의 좌상단, y 는 아래로 증가.
    /// (Unity RectTransform 은 원래 좌하단·위쪽 증가라서 여기서 한 번 바꿔 준다.)
    /// 상자 모양은 원작을 참고했다: 둥근 모서리 + 두 겹 테두리(게임보이·GBA 메시지 창), 정보 상자는 그림자와 받침선(게임보이 HP 상자의 꺾쇠).
    /// </summary>
    public static class Ui
    {
        public static readonly Color Background = Hex(0xF8F8F0);
        public static readonly Color Ink = Hex(0x202020);
        public static readonly Color Paper = Hex(0xFBFBF3);
        public static readonly Color Border = Hex(0x383838);
        public static readonly Color Outline = Hex(0x283040);
        public static readonly Color Platform = Hex(0xD0E4B0);
        public static readonly Color HpTrack = Hex(0x4A5060);
        public static readonly Color HpGreen = Hex(0x40C860);
        public static readonly Color HpYellow = Hex(0xF0C028);
        public static readonly Color HpRed = Hex(0xE84030);
        public static readonly Color HpTagText = Hex(0xF8C840);

        static Sprite frame, silhouette, hpTag;

        public static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static Image Fill(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var rt = Rect(parent, name, x, y, w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// 테두리 상자(메시지 창·메뉴). 둥근 모서리, 바깥 2px 어두운 선 + 밝은 선 + 가는 회색 선의 두 겹 테두리.
        /// 안쪽 RectTransform(내용을 붙일 곳, 가장자리에서 2px 안)을 돌려준다.
        /// </summary>
        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var outer = Fill(parent, name, x, y, w, h, Color.white);
            outer.sprite = FrameSprite;
            outer.type = Image.Type.Sliced;
            return Rect(outer.transform, "Inner", 2, 2, w - 4, h - 4);
        }

        /// <summary>
        /// 정보 상자(이름·HP): Box 에 그림자(오른쪽 아래 2px)와 받침선(포켓몬 쪽으로 뻗는 꺾쇠, 게임보이 HP 상자 참고)을 더한다.
        /// 모두 한 부모(돌려준 inner 의 parent.parent) 아래에 있어 그 부모를 끄면 함께 사라진다.
        /// </summary>
        public static RectTransform Panel(Transform parent, string name, float x, float y, float w, float h, bool ledgeToRight)
        {
            var root = Rect(parent, name, x, y, w, h);
            var shadow = Fill(root, "Shadow", 2, 2, w, h, new Color(0, 0, 0, 0.38f));
            shadow.sprite = SilhouetteSprite;
            shadow.type = Image.Type.Sliced;
            float ly = h + 4;
            if (ledgeToRight)
            {
                Fill(root, "Ledge", 8, ly, w + 4, 2, Outline);
                Fill(root, "LedgeTip", w + 12, ly - 1, 2, 4, Outline);
                Fill(root, "LedgeTip2", w + 14, ly, 2, 2, Outline);
            }
            else
            {
                Fill(root, "Ledge", -8, ly, w + 2, 2, Outline);
                Fill(root, "LedgeTip", -10, ly - 1, 2, 4, Outline);
                Fill(root, "LedgeTip2", -12, ly, 2, 2, Outline);
            }
            return Box(root, "Frame", 0, 0, w, h);
        }

        public static TextMeshProUGUI Label(Transform parent, string name, TMP_FontAsset font, float x, float y, float w, float h,
                                            TextAlignmentOptions align = TextAlignmentOptions.TopLeft, float size = 12)
        {
            var rt = Rect(parent, name, x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.color = Ink;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.lineSpacing = 4;
            return t;
        }

        /// <summary>도트 느낌의 타원(발판). 텍스처를 코드로 그린다.</summary>
        public static Image Ellipse(Transform parent, string name, float cx, float cy, int w, int h, Color color)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            float rx = w / 2f, ry = h / 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry;
                    tex.SetPixel(x, y, dx * dx + dy * dy <= 1f ? Color.white : Color.clear);
                }
            tex.Apply();
            var img = Fill(parent, name, cx - w / 2f, cy - h / 2f, w, h, color);
            img.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            return img;
        }

        // ------------------------------------------------------------------ 코드로 그리는 UI 스프라이트
        // 9-slice(Sliced) 스프라이트는 pixelsPerUnit 을 캔버스 기준값(100)과 같게 둬야 테두리 1px = 캔버스 1단위가 된다.
        const float SlicePpu = 100f;
        const int FrameSize = 12, FrameBorder = 5;

        static bool Cut(int x, int y, int n) => Mathf.Min(x, n - 1 - x) + Mathf.Min(y, n - 1 - y) < 2;   // 모서리 둥글게 (3픽셀 깎기)

        static Sprite FrameSprite => frame != null ? frame : (frame = MakeFrame(false));
        static Sprite SilhouetteSprite => silhouette != null ? silhouette : (silhouette = MakeFrame(true));

        static Sprite MakeFrame(bool silhouetteOnly)
        {
            int n = FrameSize;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Color light = Hex(0xF4F2E6), mid = Hex(0x8C94A4);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y));
                    Color c = Cut(x, y, n) ? Color.clear
                        : silhouetteOnly ? Color.white
                        : d <= 1 ? Outline : d == 2 ? light : d == 3 ? mid : Paper;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), SlicePpu, 0, SpriteMeshType.FullRect,
                                 new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder));
        }

        /// <summary>"HP:" 표 (12×8): 어두운 둥근 바탕에 노란 도트 글자 (게임보이 HP 표 참고). 글꼴이 아니라 픽셀로 찍어 선명하다.</summary>
        public static Sprite HpTagSprite => hpTag != null ? hpTag : (hpTag = MakeHpTag());

        static Sprite MakeHpTag()
        {
            const int w = 12, h = 8;
            string[] glyphs =
            {
                "101 110 0",
                "101 101 1",
                "111 110 0",
                "101 100 1",
                "101 100 0",
            };
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool corner = (x == 0 || x == w - 1) && (y == 0 || y == h - 1);
                    tex.SetPixel(x, y, corner ? Color.clear : Outline);
                }
            for (int row = 0; row < glyphs.Length; row++)
            {
                var line = glyphs[row].Replace(" ", "");
                int[] xs = { 2, 3, 4, 6, 7, 8, 10 };                 // H(2~4) P(6~8) :(10)
                for (int i = 0; i < line.Length && i < xs.Length; i++)
                    if (line[i] == '1') tex.SetPixel(xs[i], h - 2 - row, HpTagText);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
