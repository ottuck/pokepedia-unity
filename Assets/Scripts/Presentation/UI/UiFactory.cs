using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Pokepedia.Presentation.UI
{
    /// <summary>
    /// 코드로 uGUI 요소를 만드는 도우미. M1 골격용이며, M3 에서 화면을 프리팹으로 옮기면 줄어든다.
    /// 좌표 단위는 게임보이 픽셀(160×144 캔버스 기준).
    /// </summary>
    public static class UiFactory
    {
        public static readonly Color Darkest = new(0.06f, 0.22f, 0.06f);   // #0f380f
        public static readonly Color Dark = new(0.19f, 0.38f, 0.19f);      // #306230
        public static readonly Color Light = new(0.55f, 0.67f, 0.06f);     // #8bac0f
        public static readonly Color Lightest = new(0.61f, 0.74f, 0.06f);  // #9bbc0f

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PixelCanvasScaler.BaseWidth, PixelCanvasScaler.BaseHeight);
            go.GetComponent<Image>().color = color;
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, Vector2 pos, Vector2 size, float fontSize = 12, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = fontSize; t.alignment = align; t.color = Darkest; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public static Button Button(Transform parent, string name, TMP_FontAsset font, Vector2 pos, Vector2 size, UnityAction onClick, out TextMeshProUGUI label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.GetComponent<Image>().color = Dark;
            var b = go.GetComponent<Button>();
            var colors = b.colors; colors.highlightedColor = new Color(0.8f, 0.9f, 0.6f); colors.selectedColor = colors.highlightedColor; b.colors = colors;
            b.onClick.AddListener(onClick);
            label = Text(go.transform, "Label", font, Vector2.zero, size, 12);
            label.color = Lightest;
            return b;
        }
    }
}
