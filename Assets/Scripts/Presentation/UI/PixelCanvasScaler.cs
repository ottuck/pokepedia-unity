using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.UI
{
    /// <summary>
    /// 기준 해상도 240×216(게임보이와 같은 10:9, 160×144 의 1.5배)을 "정수 배율"로만 키운다.
    /// 배율이 2.7 처럼 정수가 아니면 도트 폰트·스프라이트의 픽셀 굵기가 들쭉날쭉해진다(M0 에서 확인).
    /// 160×144 가 아닌 이유: BW 스프라이트가 최대 112px(뮤츠·리자몽 앞모습)이라 1배로 넣을 공간이 필요하다(M3).
    /// HUD 캔버스와 월드 카메라(PixelCamera)가 같은 배율을 쓰도록 계산은 ScaleFor 하나로 둔다.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class PixelCanvasScaler : MonoBehaviour
    {
        public const int BaseWidth = 240;
        public const int BaseHeight = 216;

        CanvasScaler scaler;
        int lastW, lastH;

        public int Scale { get; private set; } = 1;

        /// <summary>화면(창) 크기에 들어가는 가장 큰 정수 배율 (최소 1)</summary>
        public static int ScaleFor(int width, int height)
            => Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(width / (float)BaseWidth, height / (float)BaseHeight)));

        /// <summary>
        /// 창 크기가 홀수면 화면 가운데가 화면 픽셀 경계가 아니라 픽셀 한가운데에 놓인다. 그대로 그리면 도트가 반 픽셀 걸치므로
        /// 월드 카메라와 HUD 를 이 값(화면 픽셀)만큼 같이 옮겨 격자에 맞춘다.
        /// </summary>
        public static Vector2 HalfPixelShift(int width, int height) => new Vector2(width % 2 == 1 ? 0.5f : 0f, height % 2 == 1 ? 0.5f : 0f);

        void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Apply();
        }

        void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH) Apply();
        }

        void Apply()
        {
            lastW = Screen.width; lastH = Screen.height;
            Scale = ScaleFor(lastW, lastH);
            scaler.scaleFactor = Scale;
        }
    }
}
