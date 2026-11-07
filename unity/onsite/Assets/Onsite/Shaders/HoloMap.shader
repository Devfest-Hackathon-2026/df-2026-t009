// 測量マップ風の画面効果。Cesium が描いた実写の 3D 地図を、
// モノクロの地形＋等高線＋輪郭線＋シアンの発光に置き換える（URP の Full Screen Pass 用）。
Shader "Onsite/HoloMap"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.50, 0.89, 1.0, 1)
        _Accent ("Accent", Color) = (1.0, 0.70, 0.29, 1)
        _Shadow ("Shadow", Color) = (0.02, 0.035, 0.045, 1)
        _ContourStep ("Contour step (m)", Float) = 4
        _ContourWidth ("Contour width", Range(0.2, 3)) = 0.9
        _MajorEvery ("Major contour every N", Float) = 5
        _EdgeStrength ("Edge strength", Range(0, 4)) = 1.6
        _GridSize ("Ground grid (m)", Float) = 50
        _FogStart ("Fog start (m)", Float) = 250
        _FogEnd ("Fog end (m)", Float) = 1400
        _Mono ("Photo amount", Range(0, 1)) = 0.55
        _Scan ("Scanline", Range(0, 1)) = 0.12
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

            float4 _Tint, _Accent, _Shadow;
            float _ContourStep, _ContourWidth, _MajorEvery, _EdgeStrength, _GridSize, _FogStart, _FogEnd, _Mono, _Scan;

            float3 WorldPos(float2 uv, float rawDepth)
            {
                #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif
                return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            }

            // fwidth で太さを画面上で一定にした線
            float Line(float v, float width)
            {
                float d = abs(frac(v - 0.5) - 0.5) / max(fwidth(v), 1e-5);
                return 1 - saturate(d - width * 0.5);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.texcoord;
                half3 src = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float raw = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                bool sky = raw <= 0.000001;
                #else
                bool sky = raw >= 0.999999;
                #endif

                // 背景（空）：暗い紺に、上ほど薄い光
                if (sky)
                {
                    half3 bg = lerp(_Shadow.rgb * 0.6, _Shadow.rgb * 2.2, saturate(uv.y * 0.9));
                    return half4(bg, 1);
                }

                float3 wp = WorldPos(uv, raw);
                float dist = distance(wp, _WorldSpaceCameraPos);

                // 実写の明るさだけを使い、色は抜く
                half lum = dot(src, half3(0.299, 0.587, 0.114));
                half shade = pow(saturate(lum * 1.35), 0.9);
                half3 col = lerp(_Shadow.rgb, _Tint.rgb * 0.55, shade * _Mono);

                // 等高線（高さ方向）と、地面の格子
                float h = wp.y / _ContourStep;
                float minor = Line(h, _ContourWidth);
                float major = Line(h / _MajorEvery, _ContourWidth * 1.4);
                float grid = max(Line(wp.x / _GridSize, 0.6), Line(wp.z / _GridSize, 0.6));
                col += _Tint.rgb * (minor * 0.18 + major * 0.45);
                col += _Tint.rgb * grid * 0.06;

                // 深さの差で輪郭線（建物の縁が光る）
                float2 px = _ScreenSize.zw;
                float dC = LinearEyeDepth(raw, _ZBufferParams);
                float dR = LinearEyeDepth(SampleSceneDepth(uv + float2(px.x, 0)), _ZBufferParams);
                float dU = LinearEyeDepth(SampleSceneDepth(uv + float2(0, px.y)), _ZBufferParams);
                // 細かい凹凸（木の葉など）は拾わず、建物の縁のような大きな段差だけを線にする
                float edge = smoothstep(0.35, 1.0, (abs(dR - dC) + abs(dU - dC)) / max(dC * 0.035, 0.3));
                col += _Tint.rgb * edge * _EdgeStrength * 0.6;

                // 遠くは暗く沈める
                float fog = saturate((dist - _FogStart) / max(_FogEnd - _FogStart, 1));
                col = lerp(col, _Shadow.rgb, fog * 0.85);

                // 走査線とわずかなノイズ
                float scan = 0.5 + 0.5 * sin(uv.y * _ScreenSize.y * 1.2);
                col *= 1 - _Scan * scan * 0.5;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
