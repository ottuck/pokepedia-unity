using Pokepedia.Presentation.UI;
using UnityEngine;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 월드 카메라를 HUD 와 같은 정수 배율에 맞춘다 (PROJECT.md 5.5): 월드 1 단위(= 기준 해상도 1 픽셀)가 화면의 정수 픽셀이 되게
    /// orthographicSize 를 정한다. 창 크기가 홀수면 화면 가운데가 반 픽셀 걸치므로 카메라를 반 화면 픽셀 옮겨 격자를 맞춘다
    /// (HUD 쪽은 PixelAlign 이 같은 만큼 옮긴다).
    /// Java 비교: 매 프레임 호출되는 Update() 는 게임 루프의 틱이다. 값이 바뀔 때만 다시 계산한다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class PixelCamera : MonoBehaviour
    {
        Camera cam;
        int lastW, lastH;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            Apply();
        }

        void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) Apply();
        }

        void Apply()
        {
            lastW = Screen.width; lastH = Screen.height;
            int scale = PixelCanvasScaler.ScaleFor(lastW, lastH);
            cam.orthographicSize = lastH / (2f * scale);
            var shift = PixelCanvasScaler.HalfPixelShift(lastW, lastH);
            transform.position = new Vector3(shift.x / scale, shift.y / scale, -10);
        }
    }
}
