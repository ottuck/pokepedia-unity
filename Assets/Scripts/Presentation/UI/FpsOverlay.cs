using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pokepedia.Presentation.UI
{
    /// <summary>
    /// 프레임 측정용 표시 (주소에 ?fps). 0.5초마다 평균 FPS 와 그 사이 가장 느린 프레임(ms)을 화면 왼쪽 위에 보이고,
    /// 5초 평균은 브라우저 콘솔에도 남긴다("[fps] ..."). 모바일은 화면을 보고, 데스크톱은 콘솔로도 읽는다.
    /// </summary>
    public sealed class FpsOverlay : MonoBehaviour
    {
        const float ShowEvery = 0.5f;
        const float LogEvery = 5f;

        TextMeshProUGUI label;
        float showTime, logTime, worst, logWorst;
        int showFrames, logFrames;

        public static FpsOverlay Create(TMP_FontAsset font)
        {
            var go = new GameObject("FpsOverlay", typeof(Canvas), typeof(CanvasScaler), typeof(PixelCanvasScaler));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;                       // 전투 HUD 보다 위
            Ui.Fill(go.transform, "Back", 0, 0, 96, 13, new Color(0, 0, 0, 0.6f));
            var overlay = go.AddComponent<FpsOverlay>();
            overlay.label = Ui.Label(go.transform, "Text", font, 2, 0, 94, 13);
            overlay.label.color = Color.white;
            overlay.label.textWrappingMode = TextWrappingModes.NoWrap;
            overlay.label.text = "-- FPS";
            return overlay;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            showTime += dt; logTime += dt;
            showFrames++; logFrames++;
            worst = Mathf.Max(worst, dt);
            logWorst = Mathf.Max(logWorst, dt);
            if (showTime >= ShowEvery)
            {
                label.text = $"{showFrames / showTime:0} FPS  {worst * 1000:0}ms";
                showTime = 0; showFrames = 0; worst = 0;
            }
            if (logTime >= LogEvery)
            {
                Debug.Log($"[fps] avg {logFrames / logTime:0.0} worst {logWorst * 1000:0}ms over {logTime:0.0}s ({Screen.width}x{Screen.height})");
                logTime = 0; logFrames = 0; logWorst = 0;
            }
        }
    }
}
