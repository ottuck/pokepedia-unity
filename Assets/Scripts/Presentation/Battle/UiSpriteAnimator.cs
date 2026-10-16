using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// uGUI Image 위에서 SpriteSheet 를 프레임 지연대로 반복 재생한다 (M0 결론: Animator 대신 코드 프레임 플레이어).
    /// RectTransform 의 pivot 을 바닥 중앙(0.5, 0)으로 두면 포켓몬 발 위치가 고정된다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class UiSpriteAnimator : MonoBehaviour
    {
        Image image;
        SpriteSheet sheet;
        int index;
        float timer;

        public SpriteSheet Sheet => sheet;

        void Awake() => image = GetComponent<Image>();

        public void Play(SpriteSheet newSheet)
        {
            if (image == null) image = GetComponent<Image>();
            sheet = newSheet;
            index = 0; timer = 0;
            image.enabled = sheet != null;
            if (sheet == null) return;
            image.sprite = sheet.Frames[0];
            ((RectTransform)transform).sizeDelta = sheet.Size; // 1 시트 픽셀 = 1 캔버스 단위 (정수 배율은 캔버스가 처리)
        }

        void Update()
        {
            if (sheet == null || sheet.Frames.Length < 2) return;
            timer += Time.deltaTime;
            while (timer >= sheet.Delays[index])
            {
                timer -= sheet.Delays[index];
                index = (index + 1) % sheet.Frames.Length;
            }
            image.sprite = sheet.Frames[index];
        }
    }
}
