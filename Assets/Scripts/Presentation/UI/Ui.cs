using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.UI
{
    /// <summary>
    /// 배틀 화면용 uGUI 생성 도우미. 좌표는 캔버스 픽셀(240×216), 원점은 부모의 좌상단, y 는 아래로 증가.
    /// (Unity RectTransform 은 원래 좌하단·위쪽 증가라서 여기서 한 번 바꿔 준다.)
    /// </summary>
    public static class Ui
    {
        public static readonly Color Background = Hex(0xF8F8F0);
        public static readonly Color Ink = Hex(0x202020);
        public static readonly Color Paper = Hex(0xFFFFFF);
        public static readonly Color Border = Hex(0x383838);
        public static readonly Color Platform = Hex(0xD0E4B0);
        public static readonly Color HpTrack = Hex(0x505050);
        public static readonly Color HpGreen = Hex(0x30C048);
        public static readonly Color HpYellow = Hex(0xF0C030);
        public static readonly Color HpRed = Hex(0xE83828);

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

        /// <summary>테두리 2px 상자. 안쪽 RectTransform(내용을 붙일 곳)을 돌려준다.</summary>
        public static RectTransform Box(Transform parent, string name, float x, float y, float w, float h)
        {
            var outer = Fill(parent, name, x, y, w, h, Border);
            var inner = Fill(outer.transform, "Inner", 2, 2, w - 4, h - 4, Paper);
            return (RectTransform)inner.transform;
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
    }
}
