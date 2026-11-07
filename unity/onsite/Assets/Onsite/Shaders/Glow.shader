// 加算合成で光る線・柱・輪（ブルームで滲む）。測量マップの効果のあとに描かれるよう透明キューに置く。
Shader "Onsite/Glow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.5, 0.9, 1.0, 1)
        _MainTex ("Mask", 2D) = "white" {}
        _FadeY ("Vertical fade (beam)", Range(0, 1)) = 0
        _Pulse ("Pulse speed", Float) = 0
        _Dash ("Dash repeat (0 = solid)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "RenderType" = "Transparent" }
        Blend One One
        ZWrite Off
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
                return half4(_Color.rgb * i.col.rgb * m * fade * dash * pulse * i.col.a, 1);
            }
            ENDHLSL
        }
    }
}
