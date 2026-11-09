// 加算合成で光る線・柱・輪（ブルームで滲む）。測量マップの効果のあとに描かれるよう透明キューに置く。
// 線（_Profile=1）は「レーザー」：幅方向に白い芯＋色の光、長さ方向に光の粒が流れる。
Shader "Onsite/Glow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.5, 0.9, 1.0, 1)
        _MainTex ("Mask", 2D) = "white" {}
        _FadeY ("Vertical fade (beam)", Range(0, 1)) = 0
        _Pulse ("Pulse speed", Float) = 0
        _Dash ("Dash repeat (0 = solid)", Float) = 0
        _Profile ("Laser profile across width (lines)", Range(0, 1)) = 0
        _Flow ("Flowing light packets per unit (0 = none)", Float) = 0
        _FlowSpeed ("Flow speed", Float) = 1.2
        _Core ("White core amount", Range(0, 2)) = 0.9
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "RenderType" = "Transparent" }
        Blend One One
        ZWrite Off
        ZTest [_ZTest]
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float4 _MainTex_ST; float _FadeY; float _Pulse; float _Dash;
                float _Profile; float _Flow; float _FlowSpeed; float _Core; float _ZTest;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 col : COLOR; };

            V vert(A i)
            {
                V o;
                o.pos = TransformObjectToHClip(i.pos.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                o.col = i.col;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half m = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                // 柱は上に行くほど消える
                half fade = lerp(1, saturate(1 - i.uv.y), _FadeY);
                // 破線（ルート用）
                half dash = _Dash > 0 ? step(0.5, frac(i.uv.x * _Dash - _Time.y * 0.8)) : 1;
                half pulse = _Pulse > 0 ? 0.65 + 0.35 * sin(_Time.y * _Pulse) : 1;

                half3 c = _Color.rgb;
                if (_Profile > 0)
                {
                    // 幅方向：中心ほど明るい。芯は白く、外側は色だけがにじむ
                    half x = abs(i.uv.y - 0.5) * 2;
                    half halo = exp(-x * 4.5) * (1 - x);
                    half core = smoothstep(0.28, 0.0, x);
                    // 長さ方向：光の粒が流れる（進む向きがわかる）
                    half flow = 0;
                    if (_Flow > 0)
                    {
                        half f = frac(i.uv.x * _Flow - _Time.y * _FlowSpeed);
                        flow = pow(f, 10) * 2.2;
                    }
                    half k = halo * (1 + flow) * dash;
                    c = c * k + (core * _Core * (0.6 + flow * 0.4)) * dash * half3(1, 1, 1);
                    c = lerp(_Color.rgb * dash, c, _Profile);
                }
                else c *= dash;
                return half4(c * i.col.rgb * m * fade * pulse * i.col.a, 1);
            }
            ENDHLSL
        }
    }
}
