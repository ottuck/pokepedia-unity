using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Pokepedia.Presentation.Battle
{
    /// <summary>
    /// 월드 조명 (PROJECT.md 5.5): 전역 조명(분위기 색) + 공격·피격 때 번쩍이는 점 조명(타입별 glow).
    /// 조명은 조명을 받는 머티리얼(Sprite-Lit)로 그린 배경·포켓몬에만 닿고, 이펙트·그림자·HUD 는 영향받지 않는다.
    /// 점 조명은 "더하기" 블렌드 스타일이라 색을 보탠다(밝기가 1을 넘으면 후처리 Bloom 이 번진다).
    /// </summary>
    public sealed class BattleLighting
    {
        const int AdditiveStyle = 1;   // Renderer2D 기본 Light Blend Style: 0 Multiply, 1 Additive

        readonly Light2D ambient;
        readonly Light2D[] pool = new Light2D[6];
        int next;
        int[] slotToken;   // 슬롯을 다시 쓰면 이전 Pulse 는 손을 뗀다(6개보다 많이 겹쳐도 새 조명을 끄지 않게)
        int ambientToken;

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

        /// <summary>전역 조명 색을 바로 바꾼다 (진행 중인 FadeAmbient 는 멈춘다)</summary>
        public void SetAmbient(Color color)
        {
            ambientToken++;
            ambient.color = color;
            ambient.intensity = 1f;
        }

        /// <summary>전역 조명 색을 seconds 동안 바꾼다 (승리 후 하늘이 개는 연출)</summary>
        public IEnumerator FadeAmbient(Color to, float seconds, float intensity = 1f)
        {
            int token = ++ambientToken;
            var from = ambient.color;
            float fromI = ambient.intensity;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (token != ambientToken) yield break;
                ambient.color = Color.Lerp(from, to, t / seconds);
                ambient.intensity = Mathf.Lerp(fromI, intensity, t / seconds);
                yield return null;
            }
            if (token != ambientToken) yield break;
            ambient.color = to;
            ambient.intensity = intensity;
        }

        /// <summary>화면 픽셀 위치에 점 조명을 켜고 seconds 동안 서서히 끈다 (타입별 glow). radius 는 픽셀.</summary>
        public IEnumerator Pulse(Vector2 px, Color color, float radius, float peak, float seconds)
        {
            int slot = next;
            next = (next + 1) % pool.Length;
            if (slotToken == null || slotToken.Length != pool.Length) slotToken = new int[pool.Length];
            int token = ++slotToken[slot];
            var l = pool[slot];
            l.transform.localPosition = BattleWorld.ToLocal(px);
            l.color = color;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
            l.enabled = true;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                if (token != slotToken[slot]) yield break;
                float k = 1 - t / seconds;
                l.intensity = peak * k * k;
                yield return null;
            }
            if (token == slotToken[slot]) l.enabled = false;
        }
    }
}
