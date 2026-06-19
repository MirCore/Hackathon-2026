// Full-screen blit: multiplies every pixel's alpha by _FadeProgress (global).
// Uses _MainTex (set by cmd.Blit) — avoids Blit.hlsl / TEXTURE2D_X mobile issues.
Shader "Hidden/SceneFade"
{
    Properties { _MainTex ("", 2D) = "white" {} }

    SubShader
    {
        Cull Off  ZWrite Off  ZTest Always

        Pass
        {
            Name "SceneFade"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float _FadeProgress;

            struct Attributes
            {
                float4 posOS : POSITION;
                float2 uv    : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float2 uv    : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.posCS = TransformObjectToHClip(i.posOS.xyz);
                o.uv    = i.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                col.a *= _FadeProgress;
                return col;
            }
            ENDHLSL
        }
    }
}
