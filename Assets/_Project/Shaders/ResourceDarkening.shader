Shader "_Project/Resource Darkening"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tile Tint", Color) = (1,1,1,1)
        _DarkenColor ("Darkening Color", Color) = (0,0,0,1)
        _DarkenAmount ("Darkening Amount", Range(0,1)) = 0.65
        _ResourceMask ("Resource Mask", 2D) = "black" {}
        _GridSize ("Grid Size", Vector) = (1,1,0,0)
        _BoundaryInsetPixels ("Boundary Inset (Pixels)", Float) = 50
        _TransitionPixels ("Smooth Transition (Pixels)", Range(0,200)) = 20
        _PixelsPerTile ("Pixels Per Tile", Float) = 256
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
                float4 _DarkenColor;
                float4 _GridSize;
                float _DarkenAmount;
                float _BoundaryInsetPixels;
                float _TransitionPixels;
                float _PixelsPerTile;
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
                float2 localPosition : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.localPosition = input.positionOS.xy;
                return output;
            }

            float IsResource(float2 cell)
            {
                if (any(cell < 0.0) || any(cell >= _GridSize.xy))
                {
                    return 0.0;
                }

                float2 maskUv = (cell + 0.5) / _GridSize.xy;
                return SAMPLE_TEXTURE2D(_ResourceMask, sampler_ResourceMask, maskUv).a;
            }

            float CornerTransition(float current, float diagonal, float sideA, float sideB)
            {
                // A non-resource diagonal with two resource sides forms a resource corner.
                return current * (1.0 - diagonal) * sideA * sideB;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 edgeSoftness = fwidth(input.localPosition) * 2.0;
                float2 cell = floor(input.localPosition);
                float2 cellUv = frac(input.localPosition);
                float current = IsResource(cell);
                float inset = saturate(_BoundaryInsetPixels / max(_PixelsPerTile, 1.0));
                float transition = saturate(_TransitionPixels / max(_PixelsPerTile, 1.0));
                float cornerSoftness = max(edgeSoftness.x, edgeSoftness.y);

                float left = IsResource(cell + float2(-1, 0));
                float right = IsResource(cell + float2(1, 0));
                float bottom = IsResource(cell + float2(0, -1));
                float top = IsResource(cell + float2(0, 1));
                float bottomLeft = IsResource(cell + float2(-1, -1));
                float bottomRight = IsResource(cell + float2(1, -1));
                float topLeft = IsResource(cell + float2(-1, 1));
                float topRight = IsResource(cell + float2(1, 1));

                // Fade only the resource side of a resource/non-resource border.
                float darkening = 1.0;
                darkening *= lerp(1.0, smoothstep(inset - edgeSoftness.x, inset + transition, cellUv.x), 1.0 - left);
                darkening *= lerp(1.0, smoothstep(inset - edgeSoftness.x, inset + transition, 1.0 - cellUv.x), 1.0 - right);
                darkening *= lerp(1.0, smoothstep(inset - edgeSoftness.y, inset + transition, cellUv.y), 1.0 - bottom);
                darkening *= lerp(1.0, smoothstep(inset - edgeSoftness.y, inset + transition, 1.0 - cellUv.y), 1.0 - top);

                // Cardinal checks cannot describe a diagonal corner. Use a circular
                // distance so the untouched border joins smoothly at 45-degree angles.
                float bottomLeftCorner = CornerTransition(current, bottomLeft, left, bottom);
                float bottomRightCorner = CornerTransition(current, bottomRight, right, bottom);
                float topLeftCorner = CornerTransition(current, topLeft, left, top);
                float topRightCorner = CornerTransition(current, topRight, right, top);

                darkening *= lerp(
                    1.0,
                    smoothstep(inset - cornerSoftness, inset + transition, distance(cellUv, float2(0, 0))),
                    bottomLeftCorner);
                darkening *= lerp(
                    1.0,
                    smoothstep(inset - cornerSoftness, inset + transition, distance(cellUv, float2(1, 0))),
                    bottomRightCorner);
                darkening *= lerp(
                    1.0,
                    smoothstep(inset - cornerSoftness, inset + transition, distance(cellUv, float2(0, 1))),
                    topLeftCorner);
                darkening *= lerp(
                    1.0,
                    smoothstep(inset - cornerSoftness, inset + transition, distance(cellUv, float2(1, 1))),
                    topRightCorner);

                half4 result = _DarkenColor * input.color * _Color;
                result.a *= current * darkening * _DarkenAmount;
                return result;
            }
            ENDHLSL
        }
    }
}
