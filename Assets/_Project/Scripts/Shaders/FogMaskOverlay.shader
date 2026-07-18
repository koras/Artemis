Shader "Custom/FogMaskOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _FogMaskTex("Fog Mask", 2D) = "white" {}
        _FogColor("Fog Color", Color) = (0,0,0,1)
        _FogDensity("Fog Density", Range(0, 2)) = 1
        _EdgeSoftness("Edge Softness", Range(0, 2)) = 0.5
        _NoiseTex("Noise", 2D) = "gray" {}
        _NoiseAmount("Noise Amount", Range(0, 0.5)) = 0.08
        _NoiseScale("Noise Scale", Range(0.1, 8)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_FogMaskTex);
            SAMPLER(sampler_FogMaskTex);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _FogMaskTex_ST;
                float4 _MainTex_ST;
                float4 _FogColor;
                float _FogDensity;
                float _EdgeSoftness;
                float _NoiseAmount;
                float _NoiseScale;
            CBUFFER_END

            float4 _FogMaskTex_TexelSize;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _FogMaskTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
              float2 texelSize = _FogMaskTex_TexelSize.xy;
                float radius = max(1.0, _EdgeSoftness *  3.0);
                float2 o = texelSize * radius;

                float center = SAMPLE_TEXTURE2D(_FogMaskTex, sampler_FogMaskTex, input.uv).a;

                float sideSum = 0.0;
                sideSum += SAMPLE_TEXTURE2D(_FogMaskTex, sampler_FogMaskTex, input.uv + float2(o.x, 0)).a;
                sideSum += SAMPLE_TEXTURE2D(_FogMaskTex, sampler_FogMaskTex, input.uv + float2(-o.x, 0)).a;
                sideSum += SAMPLE_TEXTURE2D(_FogMaskTex, sampler_FogMaskTex, input.uv + float2(0, o.y)).a;
                sideSum += SAMPLE_TEXTURE2D(_FogMaskTex, sampler_FogMaskTex, input.uv + float2(0, -o.y)).a;

                float diagSum = 0.0;
                diagSum += SAMPLE_TEXTURE2D(_FogMaskTex, sampler_FogMaskTex, input.uv + float2(o.x, o.y)).a;
                diagSum += SAMPLE_TEXTURE2D(_FogMaskTex, sampler_FogMaskTex, input.uv + float2(-o.x, o.y)).a;
                diagSum += SAMPLE_TEXTURE2D(_FogMaskTex, sampler_FogMaskTex, input.uv + float2(o.x, -o.y)).a;
                diagSum += SAMPLE_TEXTURE2D(_FogMaskTex, sampler_FogMaskTex, input.uv + float2(-o.x, -o.y)).a;

                float blurred = (center * 4.0 + sideSum * 2.0 + diagSum) / 16.0;
                float smoothed = lerp(center, blurred, saturate(_EdgeSoftness));

                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv * _NoiseScale).r * 2.0 - 1.0;

            float alpha = smoothstep(0.0, 1.0, smoothed);

            // плавный длинный переход к полной темноте
            float fullFog = smoothstep(0.35, 1.0, smoothed);

            alpha = lerp(alpha, 1.0, fullFog);

            if (center <= 0.01)
            {
                alpha = 0.0;
            }

                return half4(_FogColor.rgb, alpha * _FogColor.a);
            }
            ENDHLSL
        }
    }
}
