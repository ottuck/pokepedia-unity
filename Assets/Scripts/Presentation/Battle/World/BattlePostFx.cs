using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 월드 후처리 (PROJECT.md 5.5): 월드 카메라에만 걸리는 Bloom(밝은 glow 번짐)과 약한 채도 보정.
    /// HUD 는 Overlay 캔버스라 후처리를 받지 않는다. 비네트는 창 크기와 무관하게 게임 화면 가장자리에 오도록 배경이 스프라이트로 그린다.
    /// 후처리가 동작하려면 Renderer2D 에 PostProcessData 가 연결돼 있어야 한다(ProjectSetup 이 끊지 않는다).
    /// </summary>
    public sealed class BattlePostFx
    {
        public BattlePostFx(Transform parent, Camera camera)
        {
            if (camera != null) camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var go = new GameObject("PostFx", typeof(Volume));
            go.transform.SetParent(parent, false);
            var volume = go.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);   // 배경·스프라이트(≤1)는 번지지 않고, 조명으로 1을 넘긴 부분만 번진다
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.55f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(10f);
            color.contrast.Override(6f);
            volume.sharedProfile = profile;
        }
    }
}
