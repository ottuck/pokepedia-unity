using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 월드 조명 (PROJECT.md 5.5): 전역 조명(분위기 색) + 공격·피격 때 번쩍이는 점 조명(타입별 glow).
    /// 조명은 조명을 받는 머티리얼(Sprite-Lit)로 그린 배경·발판·포켓몬에만 닿고, 이펙트·그림자·HUD 는 영향받지 않는다.
    /// 점 조명은 "더하기" 블렌드 스타일이라 색을 보탠다(밝기가 1을 넘으면 후처리 Bloom 이 번진다).
    /// </summary>
    public sealed class BattleLighting
    {
        const int AdditiveStyle = 1;   // Renderer2D 기본 Light Blend Style: 0 Multiply, 1 Additive

        readonly Light2D ambient;
        readonly Light2D[] pool = new Light2D[4];
        int next;

        public BattleLighting(Transform parent, Color ambientColor, float ambientIntensity)
        {
            ambient = Create(parent, "Ambient", Light2D.LightType.Global);
            ambient.color = ambientColor;
            ambient.intensity = ambientIntensity;
            for (int i = 0; i < pool.Length; i++)
            {
                var l = Create(parent, "Glow" + i, Light2D.LightType.Point);
                l.blendStyleIndex = AdditiveStyle;
                l.falloffIntensity = 0.6f;
                l.enabled = false;
                pool[i] = l;
            }
        }

        static Light2D Create(Transform parent, string name, Light2D.LightType type)
        {
            var go = new GameObject(name, typeof(Light2D));
            go.transform.SetParent(parent, false);
            var l = go.GetComponent<Light2D>();
            l.lightType = type;
            return l;
        }

        /// <summary>화면 픽셀 위치에 점 조명을 켜고 seconds 동안 서서히 끈다 (타입별 glow). radius 는 픽셀.</summary>
        public IEnumerator Pulse(Vector2 px, Color color, float radius, float peak, float seconds)
        {
            var l = pool[next];
            next = (next + 1) % pool.Length;
            l.transform.localPosition = BattleWorld.ToLocal(px);
            l.color = color;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
            l.enabled = true;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = 1 - t / seconds;
                l.intensity = peak * k * k;
                yield return null;
            }
            l.enabled = false;
        }
    }
}
