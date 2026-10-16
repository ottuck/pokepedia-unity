using UnityEngine;

namespace Pokepedia.Presentation.UI
{
    /// <summary>
    /// HUD 영역(240×216)을 월드 카메라(PixelCamera)와 같은 반 픽셀만큼 옮겨, 창 크기가 홀수여도 월드와 HUD 의 픽셀 격자가 어긋나지 않게 한다.
    /// RectTransform 에 붙인다.
    /// </summary>
    public sealed class PixelAlign : MonoBehaviour
    {
        int lastW, lastH;

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) Apply();
        }

        void Apply()
        {
            lastW = Screen.width; lastH = Screen.height;
            int scale = PixelCanvasScaler.ScaleFor(lastW, lastH);
            var shift = PixelCanvasScaler.HalfPixelShift(lastW, lastH);
            // 카메라가 +shift 로 가면 월드 내용이 화면에서 -shift 로 움직인다 → HUD 도 같은 방향(왼쪽·아래)으로 옮긴다
            ((RectTransform)transform).anchoredPosition = new Vector2(-shift.x / scale, -shift.y / scale);
        }
    }
}
