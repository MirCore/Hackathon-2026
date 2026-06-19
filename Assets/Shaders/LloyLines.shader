Shader "Custom/LloydLines"
{
    Properties
    {
        _MainTex        ("Rock Texture",    2D)          = "white" {}
        _NormalMap      ("Normal Map",      2D)          = "bump"  {}
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
        _LinesTex       ("Lines (RenderTexture)", 2D)   = "black" {}
        _LineColor      ("Line Color",      Color)       = (1, 1, 1, 1)
        _LineStrength   ("Line Strength",   Range(0,1))  = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_LinesTex);  SAMPLER(sampler_LinesTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NormalMap_ST;
                float4 _LinesTex_ST;
                float4 _LineColor;
                float  _LineStrength;
                float  _NormalStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS     : POSITION;
                float2 uv        : TEXCOORD0;
                float3 normalOS  : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 posCS          : SV_POSITION;
                float2 uv             : TEXCOORD0;
                float3 worldPos       : TEXCOORD1;
                float3 worldNormal    : TEXCOORD2;
                float3 worldTangent   : TEXCOORD3;
                float3 worldBitangent : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.posCS   = TransformObjectToHClip(v.posOS.xyz);
                o.uv      = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = TransformObjectToWorld(v.posOS.xyz);

                VertexNormalInputs ni = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.worldNormal    = ni.normalWS;
                o.worldTangent   = ni.tangentWS;
                o.worldBitangent = ni.bitangentWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Normal map → world space
                float3 tn = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap,
                                         TRANSFORM_TEX(i.uv, _NormalMap)));
                tn.xy *= _NormalStrength;
                float3 worldN = normalize(
                    tn.x * i.worldTangent +
                    tn.y * i.worldBitangent +
                    tn.z * i.worldNormal);

                // Main light
                Light mainLight = GetMainLight();
                float3 lighting = mainLight.color * max(0.0, dot(worldN, mainLight.direction));

                // Additional lights
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; li++)
                {
                    Light light = GetAdditionalLight(li, i.worldPos);
                    lighting += light.color * max(0.0, dot(worldN, light.direction));
                }

                // Ambient
                lighting += SampleSH(worldN);

                // Rock albedo + Lloyd lines overlay
                half4 rock  = SAMPLE_TEXTURE2D(_MainTex,  sampler_MainTex,  i.uv);
                half4 lines = SAMPLE_TEXTURE2D(_LinesTex, sampler_LinesTex,
                                               TRANSFORM_TEX(i.uv, _LinesTex));
                half4 col   = lerp(rock, _LineColor, lines.r * _LineStrength);
                col.rgb *= lighting;
                col.a    = 1.0;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
