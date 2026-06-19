Shader "Custom/ObjectFade"
{
    Properties
    {
        _MainTex      ("Albedo",        2D)               = "white" {}
        _BumpMap      ("Normal Map",    2D)               = "bump"  {}
        _Color        ("Color Tint",    Color)            = (1,1,1,1)
        _FadeOffset    ("Fade Offset",    Range(0,1))       = 0
        _EdgeSoftness  ("Edge Softness", Range(0.001,0.5)) = 0.08
        [Toggle] _SquareFade ("Square Fade", Float)        = 0
        // 0 = +Y (bottom→top), 1 = −Z (back→front), 2 = +Z (front→back), 3 = −Y (top→bottom)
        [KeywordEnum(Up, Forward, Back, Down)] _FadeDir ("Reveal Direction", Float) = 0
        // Flat mode: uses raw local position instead of sphere-surface normalisation.
        // Set _FadeScale to the mesh's half-extent on the fade axis.
        [Toggle] _FlatMode  ("Flat Mode",   Float)        = 0
        _FadeScale          ("Fade Scale",  Float)        = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector"= "True"
        }

        Cull   Back
        ZWrite On
        ZTest  LEqual
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BumpMap_ST;
                float4 _Color;
                float  _FadeOffset;
                float  _EdgeSoftness;
                float  _FadeDir;
                float  _SquareFade;
                float  _FlatMode;
                float  _FadeScale;
            CBUFFER_END

            // global — not per-material, so lives outside the CBUFFER
            float _FadeProgress;

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
                float  slideValue     : TEXCOORD1;
                float3 worldNormal    : TEXCOORD2;
                float3 worldTangent   : TEXCOORD3;
                float3 worldBitangent : TEXCOORD4;
                float3 worldPos       : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.posCS  = TransformObjectToHClip(v.posOS.xyz);
                o.uv     = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = TransformObjectToWorld(v.posOS.xyz);

                float axisVal;
                if (_FlatMode > 0.5)
                {
                    // linear sweep across the mesh's local extent
                    float raw;
                    if      (_FadeDir < 0.5) raw =  v.posOS.y;
                    else if (_FadeDir < 1.5) raw = -v.posOS.z;
                    else if (_FadeDir < 2.5) raw =  v.posOS.z;
                    else                     raw = -v.posOS.y;
                    axisVal = raw / max(_FadeScale, 0.0001);
                }
                else
                {
                    // sphere-surface sweep — good for rounded 3D objects
                    float3 dir = normalize(v.posOS.xyz);
                    if      (_FadeDir < 0.5) axisVal =  dir.y;
                    else if (_FadeDir < 1.5) axisVal = -dir.z;
                    else if (_FadeDir < 2.5) axisVal =  dir.z;
                    else                     axisVal = -dir.y;
                }
                o.slideValue = axisVal * 0.5 + 0.5;

                VertexNormalInputs ni = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.worldNormal    = ni.normalWS;
                o.worldTangent   = ni.tangentWS;
                o.worldBitangent = ni.bitangentWS;

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float p = saturate(_FadeProgress + _FadeOffset);
                if (_SquareFade > 0.5) p = p * p;
                // remap [0,1] → [-0.1, 1.1] so the edge fully clears the
                // [0,1] slideValue range at both extremes
                float progress = p * 1.2 - 0.1;
                float alpha = 1.0 - smoothstep(
                    progress - _EdgeSoftness,
                    progress + _EdgeSoftness,
                    i.slideValue
                );
                clip(alpha - 0.001);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;

                float3 tn = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, TRANSFORM_TEX(i.uv, _BumpMap)));
                float3 worldN = normalize(
                    tn.x * i.worldTangent +
                    tn.y * i.worldBitangent +
                    tn.z * i.worldNormal
                );

                // main light
                Light mainLight = GetMainLight();
                float3 lighting = mainLight.color * max(0.0, dot(worldN, mainLight.direction));

                // all additional lights (your other two directional lights)
                uint lightCount = GetAdditionalLightsCount();
                for (uint li = 0u; li < lightCount; li++)
                {
                    Light light = GetAdditionalLight(li, i.worldPos);
                    lighting += light.color * max(0.0, dot(worldN, light.direction));
                }

                // spherical harmonics ambient
                lighting += SampleSH(worldN);

                col.rgb *= lighting;
                col.a   *= alpha;
                return col;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
