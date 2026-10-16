using System.Collections;
using Pokepedia.Presentation.UI;
using TMPro;
using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 하단 메시지 창. 글자를 한 자씩 찍고(타자기), 다 찍으면 ▼ 를 깜빡이며 입력을 기다린다 (PROJECT.md 5.3).
    /// 찍는 중에 입력하면 남은 글자를 한 번에 보여 준다.
    /// </summary>
    public sealed class MessageBox
    {
        const float CharsPerSecond = 45f;

        readonly TextMeshProUGUI text;
        readonly TextMeshProUGUI arrow;

        public MessageBox(RectTransform inner, TMP_FontAsset font)
        {
            text = Ui.Label(inner, "Text", font, 6, 4, inner.sizeDelta.x - 18, inner.sizeDelta.y - 6);
            arrow = Ui.Label(inner, "Next", font, inner.sizeDelta.x - 14, inner.sizeDelta.y - 16, 12, 12);
            arrow.text = "▼";
            arrow.enabled = false;
        }

        /// <summary>입력을 기다리지 않는 문구 (예: 메뉴와 함께 띄우는 "무엇을 할까?")</summary>
        public void SetStatic(string message)
        {
            text.text = message;
            text.maxVisibleCharacters = int.MaxValue;
            arrow.enabled = false;
        }

        public void Clear() => SetStatic("");

        /// <summary>타자기로 출력하고 확인 입력을 기다린다.</summary>
        public IEnumerator Say(string message)
        {
            text.text = message;
            text.ForceMeshUpdate();
            int total = text.textInfo.characterCount;
            arrow.enabled = false;
            float shown = 0;
            text.maxVisibleCharacters = 0;
            yield return null; // 앞 메시지를 넘긴 입력이 이 메시지를 바로 건너뛰지 않게 한 프레임 쉰다
            while (shown < total)
            {
                if (BattleInput.Advance) { shown = total; }
                else shown += CharsPerSecond * Time.deltaTime;
                text.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(shown));
                yield return null;
            }
            text.maxVisibleCharacters = int.MaxValue;
            yield return null;
            float blink = 0;
            while (!BattleInput.Advance)
            {
                blink += Time.deltaTime;
                arrow.enabled = (blink % 0.8f) < 0.5f;
                yield return null;
            }
            arrow.enabled = false;
        }
    }
}
