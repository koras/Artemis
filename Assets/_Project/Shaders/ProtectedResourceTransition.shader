Shader "_Project/Protected Resource Transition"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tile Tint", Color) = (1,1,1,1)
        _LineColor ("Line Color", Color) = (0.2,0.9,1,1)
        _ResourceMask ("Protected Resource ID Mask", 2D) = "black" {}
        _GridSize ("Grid Size", Vector) = (1,1,0,0)
        _Smoothing ("Smoothing", Range(0.001, 0.5)) = 0.08
        _LineOffset ("Line Offset (0 = centre)", Range(-0.25, 0.5)) = 0
        _LineThickness ("Line Thickness", Range(0.001, 0.25)) = 0.035
        _CornerRadius ("Corner Radius", Range(0.001, 0.5)) = 0.08
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.25)) = 0.04
        _WaveFrequency ("Wave Frequency", Range(0.1, 20)) = 5
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
                float4 _LineColor;
                float4 _GridSize;
                float _Smoothing;
                float _LineOffset;
                float _LineThickness;
                float _CornerRadius;
                float _WaveAmplitude;
                float _WaveFrequency;
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

            float ResourceId(float2 cell)
            {
                if (any(cell < 0.0) || any(cell >= _GridSize.xy))
                {
                    return 0.0;
                }

                float2 maskUv = (cell + 0.5) / _GridSize.xy;
                return SAMPLE_TEXTURE2D(_ResourceMask, sampler_ResourceMask, maskUv).r * 4.0;
            }

            float IsDifferentResource(float first, float second)
            {
                return step(0.5, first) * step(0.5, second)
                    * (1.0 - step(0.01, 1.0 - abs(first - second)));
            }

            float Line(float distanceToLine)
            {
                return 1.0 - smoothstep(_LineThickness, _LineThickness + _Smoothing, abs(distanceToLine));
            }

            float Wave(float coordinate)
            {
                float phase = coordinate * _WaveFrequency * 6.2831853;
                return (sin(phase) + sin(phase * 0.47 + 2.1) * 0.35) * 0.5 * _WaveAmplitude;
            }

            float Corner(float current, float diagonal, float sideA, float sideB)
            {
                return IsDifferentResource(current, diagonal)
                    * step(0.01, 1.0 - abs(current - sideA))
                    * step(0.01, 1.0 - abs(current - sideB));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 cell = floor(input.localPosition);
                float2 uv = frac(input.localPosition);
                float current = ResourceId(cell);
                float waveOffset = Wave(cell.x + cell.y + uv.x + uv.y);

                float left = IsDifferentResource(current, ResourceId(cell + float2(-1, 0)));
                float right = IsDifferentResource(current, ResourceId(cell + float2(1, 0)));
                float bottom = IsDifferentResource(current, ResourceId(cell + float2(0, -1)));
                float top = IsDifferentResource(current, ResourceId(cell + float2(0, 1)));

                float boundaryAmount = max(max(Line(uv.x - (_LineOffset + waveOffset)) * left, Line((1.0 - uv.x) - (_LineOffset + waveOffset)) * right),
                                 max(Line(uv.y - (_LineOffset + waveOffset)) * bottom, Line((1.0 - uv.y) - (_LineOffset + waveOffset)) * top));

                float cornerLine = 0.0;
                cornerLine = max(cornerLine, (1.0 - smoothstep(0.0, _CornerRadius, distance(uv, float2(0, 0))))
                    * Corner(current, ResourceId(cell + float2(-1, -1)), ResourceId(cell + float2(-1, 0)), ResourceId(cell + float2(0, -1))));
                cornerLine = max(cornerLine, (1.0 - smoothstep(0.0, _CornerRadius, distance(uv, float2(1, 0))))
                    * Corner(current, ResourceId(cell + float2(1, -1)), ResourceId(cell + float2(1, 0)), ResourceId(cell + float2(0, -1))));
                cornerLine = max(cornerLine, (1.0 - smoothstep(0.0, _CornerRadius, distance(uv, float2(0, 1))))
                    * Corner(current, ResourceId(cell + float2(-1, 1)), ResourceId(cell + float2(-1, 0)), ResourceId(cell + float2(0, 1))));
                cornerLine = max(cornerLine, (1.0 - smoothstep(0.0, _CornerRadius, distance(uv, float2(1, 1))))
                    * Corner(current, ResourceId(cell + float2(1, 1)), ResourceId(cell + float2(1, 0)), ResourceId(cell + float2(0, 1))));

                half4 result = _LineColor * input.color * _Color;
                result.a *= saturate(max(boundaryAmount, cornerLine));
                return result;
            }
            ENDHLSL
        }
    }
}
