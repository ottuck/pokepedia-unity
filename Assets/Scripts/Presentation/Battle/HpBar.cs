using System.Collections;
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// HP 바(초록 &gt; 50% &gt; 노랑 &gt; 20% &gt; 빨강)와 선택적인 "현재/최대" 숫자. 피해를 받으면 일정 속도로 줄어든다.
    /// 모양은 원작 참고: 어두운 "HP:" 표 + 어두운 테두리 안의 바, 바 위 1px 은 밝게·아래 1px 은 어둡게 해 입체감을 준다.
    /// MonoBehaviour 가 아닌 일반 클래스: 코루틴은 화면(BattleScreen)이 돌린다.
    /// </summary>
    public sealed class HpBar
    {
        const int TagW = 12, BarH = 8;

        readonly RectTransform fill;
        readonly Image fillImage;
        readonly TextMeshProUGUI numbers;
        readonly float width;
        int max = 1;

        public int Shown { get; private set; }

        public HpBar(Transform parent, TMP_FontAsset font, float x, float y, float width, bool showNumbers)
        {
            this.width = width;
            var tag = Ui.Fill(parent, "HpTag", x, y, TagW, BarH, Color.white);
            tag.sprite = Ui.HpTagSprite;
            float bx = x + TagW + 1;
            Ui.Fill(parent, "HpFrame", bx, y, width + 4, BarH, Ui.Outline);
            Ui.Fill(parent, "HpTrack", bx + 1, y + 1, width + 2, BarH - 2, Ui.HpTrack);
            fillImage = Ui.Fill(parent, "HpFill", bx + 2, y + 2, width, BarH - 4, Ui.HpGreen);
            fill = (RectTransform)fillImage.transform;
            Edge(fill, true, new Color(1, 1, 1, 0.5f));
            Edge(fill, false, new Color(0, 0, 0, 0.22f));
            if (showNumbers) numbers = Ui.Label(parent, "HpNumbers", font, x, y + BarH + 1, width + TagW + 6, 14, TextAlignmentOptions.TopRight);
        }

        /// <summary>채움의 위(밝게)·아래(어둡게) 1px 줄. 채움 폭을 따라 늘어난다.</summary>
        static void Edge(RectTransform parent, bool top, Color color)
        {
            var go = new GameObject(top ? "Highlight" : "Shade", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0, top ? 1 : 0);
            rt.anchorMax = new Vector2(1, top ? 1 : 0);
            rt.pivot = new Vector2(0.5f, top ? 1 : 0);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0, 1);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        public void Set(int hp, int maxHp)
        {
            max = Mathf.Max(1, maxHp);
            Shown = hp;
            Apply();
        }

        /// <summary>현재 표시값에서 목표 HP 까지 줄인다(늘린다). 체력이 많을수록 오래 걸리지 않도록 최대 시간을 둔다.</summary>
        public IEnumerator AnimateTo(int hp)
        {
            int from = Shown;
            if (from == hp) yield break;
            float duration = Mathf.Clamp(Mathf.Abs(from - hp) / (float)max * 1.2f, 0.25f, 0.9f);
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                Shown = Mathf.RoundToInt(Mathf.Lerp(from, hp, t / duration));
                Apply();
                yield return null;
            }
            Shown = hp;
            Apply();
        }

        void Apply()
        {
            float ratio = Mathf.Clamp01(Shown / (float)max);
            fill.sizeDelta = new Vector2(Mathf.Round(width * ratio), fill.sizeDelta.y);
            fillImage.color = ratio > 0.5f ? Ui.HpGreen : ratio > 0.2f ? Ui.HpYellow : Ui.HpRed;
            if (numbers != null) numbers.text = $"{Shown}/ {max}";
        }
    }
}
