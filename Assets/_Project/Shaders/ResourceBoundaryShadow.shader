Shader "_Project/Resource Boundary Shadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ResourceMask ("Resource Mask", 2D) = "white" {}
        _GridSize ("Grid Size", Vector) = (1,1,0,0)
        _Smoothing ("Smoothing", Range(0.001, 0.5)) = 0.08
        _BorderInset ("Border Inset", Range(0, 0.5)) = 0.12
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.25)) = 0.04
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_ResourceMask); SAMPLER(sampler_ResourceMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _GridSize;
                float _Smoothing;
                float _BorderInset;
                float _WaveAmplitude;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                output.localPosition = input.positionOS.xy;
                return output;
            }

            float IsResource(float2 cell)
            {
                // Cells outside the generated grid are treated as empty, so map edges also receive a border.
                if (any(cell < 0.0) || any(cell >= _GridSize.xy))
                {
                    return 0.0;
                }

                float2 maskUv = (cell + 0.5) / _GridSize.xy;
                return SAMPLE_TEXTURE2D(_ResourceMask, sampler_ResourceMask, maskUv).a;
            }

            float Wave(float coordinate, float seed)
            {
                // A deterministic combination of two frequencies gives a stable irregular contour.
                return (sin(coordinate * 5.17 + seed * 2.41)
                        + sin(coordinate * 11.73 + seed * 5.83) * 0.35) * 0.5;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color * _Color;
                float2 cell = floor(input.localPosition);
                float2 cellUv = frac(input.localPosition);
                float resource = IsResource(cell);

                // A side is a boundary only when the adjacent cell is not a resource.
                float leftBoundary = 1.0 - IsResource(cell + float2(-1, 0));
                float rightBoundary = 1.0 - IsResource(cell + float2(1, 0));
                float bottomBoundary = 1.0 - IsResource(cell + float2(0, -1));
                float topBoundary = 1.0 - IsResource(cell + float2(0, 1));

                float2 worldPosition = cell + cellUv;
                float leftInset = saturate(_BorderInset + Wave(worldPosition.y, 1.0) * _WaveAmplitude);
                float rightInset = saturate(_BorderInset + Wave(worldPosition.y, 2.0) * _WaveAmplitude);
                float bottomInset = saturate(_BorderInset + Wave(worldPosition.x, 3.0) * _WaveAmplitude);
                float topInset = saturate(_BorderInset + Wave(worldPosition.x, 4.0) * _WaveAmplitude);

                float leftFade = smoothstep(leftInset, leftInset + _Smoothing, cellUv.x);
                float rightFade = smoothstep(rightInset, rightInset + _Smoothing, 1.0 - cellUv.x);
                float bottomFade = smoothstep(bottomInset, bottomInset + _Smoothing, cellUv.y);
                float topFade = smoothstep(topInset, topInset + _Smoothing, 1.0 - cellUv.y);
                float boundaryFade = min(min(lerp(1.0, leftFade, leftBoundary), lerp(1.0, rightFade, rightBoundary)),
                                         min(lerp(1.0, bottomFade, bottomBoundary), lerp(1.0, topFade, topBoundary)));

                // Side fades alone produce a square corner. For a real resource corner,
                // also fade from the diagonal corner when both adjacent sides are resources.
                float bottomLeftCornerBoundary =
                    (1.0 - IsResource(cell + float2(-1, -1)))
                    * IsResource(cell + float2(-1, 0))
                    * IsResource(cell + float2(0, -1));
                float bottomRightCornerBoundary =
                    (1.0 - IsResource(cell + float2(1, -1)))
                    * IsResource(cell + float2(1, 0))
                    * IsResource(cell + float2(0, -1));
                float topLeftCornerBoundary =
                    (1.0 - IsResource(cell + float2(-1, 1)))
                    * IsResource(cell + float2(-1, 0))
                    * IsResource(cell + float2(0, 1));
                float topRightCornerBoundary =
                    (1.0 - IsResource(cell + float2(1, 1)))
                    * IsResource(cell + float2(1, 0))
                    * IsResource(cell + float2(0, 1));

                float bottomLeftDistance = distance(cellUv, float2(0.0, 0.0));
                float bottomRightDistance = distance(cellUv, float2(1.0, 0.0));
                float topLeftDistance = distance(cellUv, float2(0.0, 1.0));
                float topRightDistance = distance(cellUv, float2(1.0, 1.0));
                float bottomLeftFade = smoothstep(_BorderInset, _BorderInset + _Smoothing, bottomLeftDistance);
                float bottomRightFade = smoothstep(_BorderInset, _BorderInset + _Smoothing, bottomRightDistance);
                float topLeftFade = smoothstep(_BorderInset, _BorderInset + _Smoothing, topLeftDistance);
                float topRightFade = smoothstep(_BorderInset, _BorderInset + _Smoothing, topRightDistance);

                boundaryFade = min(boundaryFade, lerp(1.0, bottomLeftFade, bottomLeftCornerBoundary));
                boundaryFade = min(boundaryFade, lerp(1.0, bottomRightFade, bottomRightCornerBoundary));
                boundaryFade = min(boundaryFade, lerp(1.0, topLeftFade, topLeftCornerBoundary));
                boundaryFade = min(boundaryFade, lerp(1.0, topRightFade, topRightCornerBoundary));

                // The layer is intentionally subtle so the resource sprite remains readable underneath.
                float shadow = resource * boundaryFade * 0.42;
                source.rgb *= 1.0 - shadow;
                return source;
            }
            ENDHLSL
        }
    }
}
