Shader "Custom/OrganicTileClip"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _EdgeSize ("Edge Size", Range(0.01, 0.4)) = 0.10
        _Softness ("Softness", Range(0.001, 0.2)) = 0.08
        _NoiseStrength ("Noise Strength", Range(0, 0.2)) = 0.02
        _OutlineWidth ("Outline Width", Range(0.001, 0.2)) = 0.03
        _OutlineStrength ("Outline Strength", Range(0, 1)) = 1
        _OutlineNoise ("Outline Noise", Range(0, 0.2)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Pass
        {
            Name "ForwardLit"

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;

            float _EdgeSize;
            float _Softness;
            float _NoiseStrength;
            float _OutlineWidth;
            float _OutlineStrength;
            float _OutlineNoise;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;

                return OUT;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(a, b, u.x),
                    lerp(c, d, u.x),
                    u.y
                );
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Базовый цвет тайла из атласа/спрайта.
                half4 col = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    IN.uv
                );

                // R-канал vertex color используется только как закодированная маска соседей (0..15)/15.
                // Не умножаем цвет тайла на IN.color, чтобы маска не затемняла/не искажала текстуру.
                int mask = round(IN.color.r * 15.0);

                // Биты маски:
                // 1 = up, 2 = right, 4 = down, 8 = left.
                bool up    = (mask & 1) != 0;
                bool right = (mask & 2) != 0;
                bool down  = (mask & 4) != 0;
                bool left  = (mask & 8) != 0;

                float2 uv = IN.uv;

                // Небольшой procedural noise, чтобы край выглядел менее "идеально прямым".
                float n = noise(uv * 12.0) - 0.5;

                float edge =
                    _EdgeSize +
                    n * _NoiseStrength;

                // Начинаем с полностью непрозрачного тайла и постепенно режем alpha
                // только по тем сторонам, где нет соседней клетки.
                float alpha = 1.0;

                if (!left)
                {
                    alpha *= smoothstep(
                        0.0,
                        _Softness,
                        uv.x - edge
                    );
                }

                if (!right)
                {
                    alpha *= smoothstep(
                        0.0,
                        _Softness,
                        1.0 - uv.x - edge
                    );
                }

                if (!down)
                {
                    alpha *= smoothstep(
                        0.0,
                        _Softness,
                        uv.y - edge
                    );
                }

                if (!up)
                {
                    alpha *= smoothstep(
                        0.0,
                        _Softness,
                        1.0 - uv.y - edge
                    );
                }

                // Применяем итоговую маску прозрачности к исходной альфе текстуры.
                col.a *= alpha;

                // Неровная черная обводка только по открытым краям (где нет соседнего тайла).
                float outline = 0.0;

                if (!left)
                {
                    float leftNoise = noise(float2(uv.y * 10.0, uv.x * 4.0)) - 0.5;
                    float leftBand = 1.0 - smoothstep(
                        edge,
                        edge + _OutlineWidth + leftNoise * _OutlineNoise,
                        uv.x
                    );
                    outline = max(outline, leftBand);
                }

                if (!right)
                {
                    float rightNoise = noise(float2(uv.y * 10.0 + 13.0, uv.x * 4.0)) - 0.5;
                    float rightBand = 1.0 - smoothstep(
                        edge,
                        edge + _OutlineWidth + rightNoise * _OutlineNoise,
                        1.0 - uv.x
                    );
                    outline = max(outline, rightBand);
                }

                if (!down)
                {
                    float downNoise = noise(float2(uv.x * 10.0 + 29.0, uv.y * 4.0)) - 0.5;
                    float downBand = 1.0 - smoothstep(
                        edge,
                        edge + _OutlineWidth + downNoise * _OutlineNoise,
                        uv.y
                    );
                    outline = max(outline, downBand);
                }

                if (!up)
                {
                    float upNoise = noise(float2(uv.x * 10.0 + 47.0, uv.y * 4.0)) - 0.5;
                    float upBand = 1.0 - smoothstep(
                        edge,
                        edge + _OutlineWidth + upNoise * _OutlineNoise,
                        1.0 - uv.y
                    );
                    outline = max(outline, upBand);
                }

                outline *= saturate(alpha) * _OutlineStrength;
                col.rgb = lerp(col.rgb, half3(0.0, 0.0, 0.0), outline);

                // Отбрасываем почти прозрачные пиксели.
                clip(col.a - 0.01);

                return col;
            }

            ENDHLSL
        }
    }
}
