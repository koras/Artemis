Shader "_Project/Resource Boundary Wave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tile Tint", Color) = (1,1,1,1)
        _WaveColor ("Wave Color", Color) = (0.2,0.9,1,1)
        _ResourceMask ("Resource Mask", 2D) = "white" {}
        _GridSize ("Grid Size", Vector) = (1,1,0,0)
        _Smoothing ("Smoothing", Range(0.001, 0.5)) = 0.08
        _BorderInset ("Boundary Offset (0 = centre)", Range(-0.25, 0.5)) = 0
        _WaveThickness ("Wave Thickness", Range(0.001, 0.25)) = 0.035
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
                float4 _WaveColor;
                float4 _GridSize;
                float _Smoothing;
                float _BorderInset;
                float _WaveThickness;
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

            float IsResource(float2 cell)
            {
                // Outside the generated grid is empty, which also outlines map edges.
                if (any(cell < 0.0) || any(cell >= _GridSize.xy))
                {
                    return 0.0;
                }

                float2 maskUv = (cell + 0.5) / _GridSize.xy;
                return SAMPLE_TEXTURE2D(_ResourceMask, sampler_ResourceMask, maskUv).a;
            }

            float Wave(float coordinate, float seed)
            {
                // World-space coordinates keep neighbouring tile segments continuous.
                float phase = coordinate * _WaveFrequency * 6.2831853 + seed * 1.37;
                return (sin(phase) + sin(phase * 0.47 + 2.1) * 0.35) * 0.5;
            }

            float Line(float distanceToLine)
            {
                return 1.0 - smoothstep(_WaveThickness, _WaveThickness + _Smoothing, abs(distanceToLine));
            }

            float CornerTransition(float current, float diagonal, float sideA, float sideB)
            {
                // Selects a one-cell corner pattern: the diagonal differs while both
                // orthogonal cells have the current state.
                return abs(current - diagonal)
                    * (1.0 - abs(current - sideA))
                    * (1.0 - abs(current - sideB));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tile = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color * _Color;
                float2 cell = floor(input.localPosition);
                float2 cellUv = frac(input.localPosition);
                float resource = IsResource(cell);
                float2 worldPosition = cell + cellUv;

                // XOR marks both sides of a resource/non-resource transition.
                // This lets the overlay draw half of the line into the neighbouring cell.
                float leftBoundary = abs(resource - IsResource(cell + float2(-1, 0)));
                float rightBoundary = abs(resource - IsResource(cell + float2(1, 0)));
                float bottomBoundary = abs(resource - IsResource(cell + float2(0, -1)));
                float topBoundary = abs(resource - IsResource(cell + float2(0, 1)));

                // Every side uses the same continuous world-space wave. This guarantees
                // that the two segments have the same phase at a shared corner.
                float waveOffset = Wave(worldPosition.x + worldPosition.y, 0.0) * _WaveAmplitude;
                float leftLine = Line(cellUv.x - (_BorderInset + waveOffset));
                float rightLine = Line((1.0 - cellUv.x) - (_BorderInset + waveOffset));
                float bottomLine = Line(cellUv.y - (_BorderInset + waveOffset));
                float topLine = Line((1.0 - cellUv.y) - (_BorderInset + waveOffset));
                float boundaryLine = max(max(leftLine * leftBoundary, rightLine * rightBoundary),
                                         max(bottomLine * bottomBoundary, topLine * topBoundary));

                // Close the shared corner when two boundary sides meet. The small join
                // prevents pixel gaps without creating a large circular protrusion.
                float cornerJoinRadius = _WaveThickness + _Smoothing;
                float bottomLeftJoin = 1.0 - smoothstep(
                    0.0,
                    cornerJoinRadius,
                    distance(cellUv, float2(0, 0)));
                float bottomRightJoin = 1.0 - smoothstep(
                    0.0,
                    cornerJoinRadius,
                    distance(cellUv, float2(1, 0)));
                float topLeftJoin = 1.0 - smoothstep(
                    0.0,
                    cornerJoinRadius,
                    distance(cellUv, float2(0, 1)));
                float topRightJoin = 1.0 - smoothstep(
                    0.0,
                    cornerJoinRadius,
                    distance(cellUv, float2(1, 1)));

                boundaryLine = max(boundaryLine, bottomLeftJoin * leftBoundary * bottomBoundary);
                boundaryLine = max(boundaryLine, bottomRightJoin * rightBoundary * bottomBoundary);
                boundaryLine = max(boundaryLine, topLeftJoin * leftBoundary * topBoundary);
                boundaryLine = max(boundaryLine, topRightJoin * rightBoundary * topBoundary);

                // The turn can belong to the diagonal cell rather than the cell that
                // owns either side. Evaluate all four 2x2 corner patterns as well.
                float left = IsResource(cell + float2(-1, 0));
                float right = IsResource(cell + float2(1, 0));
                float bottom = IsResource(cell + float2(0, -1));
                float top = IsResource(cell + float2(0, 1));
                float bottomLeft = IsResource(cell + float2(-1, -1));
                float bottomRight = IsResource(cell + float2(1, -1));
                float topLeft = IsResource(cell + float2(-1, 1));
                float topRight = IsResource(cell + float2(1, 1));

                float bottomLeftTurn = CornerTransition(resource, bottomLeft, left, bottom);
                float bottomRightTurn = CornerTransition(resource, bottomRight, right, bottom);
                float topLeftTurn = CornerTransition(resource, topLeft, left, top);
                float topRightTurn = CornerTransition(resource, topRight, right, top);

                boundaryLine = max(boundaryLine, bottomLeftJoin * bottomLeftTurn);
                boundaryLine = max(boundaryLine, bottomRightJoin * bottomRightTurn);
                boundaryLine = max(boundaryLine, topLeftJoin * topLeftTurn);
                boundaryLine = max(boundaryLine, topRightJoin * topRightTurn);

                // The overlay reuses resource tile geometry, but the boundary must not inherit
                // the resource texture alpha or its rock pattern.
                tile.rgb = _WaveColor.rgb;
                tile.a = saturate(boundaryLine * _WaveColor.a);
                return tile;
            }
            ENDHLSL
        }
    }
}
