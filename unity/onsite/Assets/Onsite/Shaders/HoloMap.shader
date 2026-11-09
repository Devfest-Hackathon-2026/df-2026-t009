// デス・ストランディングの地図端末のような画面効果（URP の Full Screen Pass 用）。
// ・地形は実写の色を残した「立体模型」：彩度を少し落とし、陰影を強めて起伏を見せる
// ・見えている範囲の外側は、四角いブロック（ボクセル）単位でモザイク化しながら欠け、闇に沈む
// ・闇は深い紺に、うすい格子
// 中心と広さは OrbitCamera が _OnsiteFocus / _OnsiteReveal で渡す。
Shader "Onsite/HoloMap"
{
    Properties
    {
        _Bg ("Void", Color) = (0.012, 0.024, 0.036, 1)
        _GridColor ("Void grid", Color) = (0.20, 0.55, 0.75, 1)
        _Tint ("Line tint", Color) = (0.45, 0.85, 1.0, 1)
        _Sat ("Saturation", Range(0, 1.5)) = 0.68
        _Contrast ("Contrast", Range(0.5, 2)) = 1.3
        _Exposure ("Exposure", Range(0.3, 2)) = 0.82
        _Relief ("Relief shading", Range(0, 1)) = 0.55
        _EdgeStrength ("Edge strength", Range(0, 2)) = 0.08
        _FogStart ("Fog start (m)", Float) = 900
        _FogEnd ("Fog end (m)", Float) = 3200
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "HoloMap"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _Bg, _GridColor, _Tint;
            float _Sat, _Contrast, _Exposure, _Relief, _EdgeStrength, _FogStart, _FogEnd;
            float4 _OnsiteFocus;   // xyz: 見ている中心
            float4 _OnsiteReveal;  // x: はっきり見える半径, y: 欠けきる半径, z: ブロックの大きさ(m)
            float _OnsiteRaw;      // 1 なら効果なし（確認用）

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float3 WorldPos(float2 uv, float rawDepth)
            {
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            }

            bool IsSky(float raw)
            {
                #if UNITY_REVERSED_Z
                return raw <= 0.000001;
                #else
                return raw >= 0.999999;
                #endif
            }

            // 闇：深い紺、うすい格子、中央がわずかに明るい
            half3 Void(float2 uv)
            {
                float2 px = uv * _ScreenSize.xy;
                float cell = 56;
                float2 g = abs(frac(px / cell) - 0.5) * cell;
                float grid = 1 - saturate(min(g.x, g.y) - 0.5);
                // ところどころ、少し明るい四角（ボクセルの残り）
                float2 id = floor(px / cell);
                float speck = step(0.93, Hash(id)) * 0.035;
                float vig = 1 - saturate(length(uv - 0.5) * 1.1);
                half3 c = _Bg.rgb * (0.7 + vig * 0.8);
                c += _GridColor.rgb * (grid * 0.045 + speck);
                return c;
            }

            // 実写の色を「立体模型」の色へ
            half3 Grade(half3 c)
            {
                c *= _Exposure;
                half l = dot(c, half3(0.299, 0.587, 0.114));
                c = lerp(l.xxx, c, _Sat);
                c = (c - 0.38) * _Contrast + 0.38;
                // 影は青く、明るい所はわずかに暖かく
                half t = saturate(l * 1.6);
                c *= lerp(half3(0.80, 0.92, 1.05), half3(1.06, 1.0, 0.92), t);
                return max(c, 0);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                if (_OnsiteRaw > 0.5) return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0);
                float raw = SampleSceneDepth(uv);
                if (IsSky(raw)) return half4(Void(uv), 1);

                float3 wp = WorldPos(uv, raw);
                float camDist = distance(wp, _WorldSpaceCameraPos);
                // 起伏の陰影用の法線（分岐の前に求める）
                float3 nrm = normalize(cross(ddy(wp), ddx(wp)));
                nrm *= nrm.y < 0 ? -1 : 1;

                // どこまで見せるか：地面のブロック単位で、少しずつ揺らいだ境目
                float inner = max(_OnsiteReveal.x, 1);
                float outer = max(_OnsiteReveal.y, inner + 1);
                float cellSize = max(_OnsiteReveal.z, 1);
                float2 cell = floor(wp.xz / cellSize);
                float2 cc = (cell + 0.5) * cellSize;
                float d = distance(cc, _OnsiteFocus.xz);
                float n = Hash(cell);
                float t = (d - inner) / (outer - inner) + (n - 0.5) * 0.55;

                if (t >= 1) return half4(Void(uv), 1);

                // 欠けかけの所は、画面上で粗いモザイクにする（ボクセルの塊に見える）
                float2 suv = uv;
                if (t > 0)
                {
                    float block = lerp(3, 26, saturate(t)) ;
                    float2 bp = (floor(uv * _ScreenSize.xy / block) + 0.5) * block;
                    suv = bp * _ScreenSize.zw;
                }
                half3 src = t > 0
                    ? SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, suv, 0).rgb
                    : SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb;
                half3 col = Grade(src);

                // 起伏の陰影（深さから法線を作る）：斜め上の光で山肌や建物の面を立たせる
                float3 L = normalize(float3(-0.45, 0.75, 0.35));
                float lit = saturate(dot(nrm, L));
                col *= lerp(1, lerp(0.55, 1.25, lit), _Relief);

                // 大きな段差（建物の縁）だけ、ごく細い青い線
                float2 px = _ScreenSize.zw;
                float dC = LinearEyeDepth(raw, _ZBufferParams);
                float dR = LinearEyeDepth(SampleSceneDepth(uv + float2(px.x, 0)), _ZBufferParams);
                float dU = LinearEyeDepth(SampleSceneDepth(uv + float2(0, px.y)), _ZBufferParams);
                float edge = smoothstep(0.4, 1.0, (abs(dR - dC) + abs(dU - dC)) / max(dC * 0.04, 0.3));
                col += _Tint.rgb * edge * _EdgeStrength;

                // 欠けかけのブロックは暗く沈め、ブロックの縁に細い溝
                if (t > 0)
                {
                    float2 f = abs(frac(wp.xz / cellSize) - 0.5);
                    float groove = smoothstep(0.44, 0.5, max(f.x, f.y));
                    float sink = saturate(t);
                    col = lerp(col, Void(uv), sink * 0.75);
                    col *= 1 - groove * 0.5 * sink;
                    // ところどころ、青く光るブロック
                    col += _Tint.rgb * step(0.97, n) * 0.06 * (1 - sink);
                }

                // 遠くはうすく霞む
                float fog = saturate((camDist - _FogStart) / max(_FogEnd - _FogStart, 1));
                col = lerp(col, Void(uv), fog * 0.8);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
