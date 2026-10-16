// 피격 번쩍임용 스프라이트 셰이더 (월드 렌더링, PROJECT.md 5.5). 스프라이트의 알파 모양 그대로 _FlashColor(기본 흰색)로 칠한다.
// 본체 위에 겹치는 두 번째 SpriteRenderer 에 쓴다: SpriteRenderer.color 의 알파가 번쩍임 세기(0~1)다.
// 본체는 조명을 받는 머티리얼, 번쩍임은 조명과 무관한 언릿 — 번쩍임은 조명에 어두워지면 안 된다.
// LightMode 태그가 없으면 URP 2D 렌더러가 "SRPDefaultUnlit" 패스로 그린다.
Shader "Pokepedia/Sprite Flash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" "RenderPipeline"="UniversalPipeline" }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SpriteFlash"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _FlashColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
                return half4(_FlashColor.rgb, a * _FlashColor.a);
            }
            ENDHLSL
        }
    }
}
