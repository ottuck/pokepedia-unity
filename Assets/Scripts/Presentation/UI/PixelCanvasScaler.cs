using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.UI
{
    /// <summary>
    /// 기준 해상도 240×216(게임보이와 같은 10:9, 160×144 의 1.5배)을 "정수 배율"로만 키운다.
    /// 배율이 2.7 처럼 정수가 아니면 도트 폰트·스프라이트의 픽셀 굵기가 들쭉날쭉해진다(M0 에서 확인).
    /// 160×144 가 아닌 이유: BW 스프라이트가 최대 112px(뮤츠·리자몽 앞모습)이라 1배로 넣을 공간이 필요하다(M3).
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class PixelCanvasScaler : MonoBehaviour
    {
        public const int BaseWidth = 240;
        public const int BaseHeight = 216;

        CanvasScaler scaler;
        int lastW, lastH;

        public int Scale { get; private set; } = 1;

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
            Scale = Mathf.Max(1, Mathf.FloorToInt(Mathf.Min(lastW / (float)BaseWidth, lastH / (float)BaseHeight)));
            scaler.scaleFactor = Scale;
        }
    }
}
