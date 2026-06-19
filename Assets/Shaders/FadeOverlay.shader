// Sphere overlay that covers the scene when _FadeProgress = 0
// and becomes fully transparent when _FadeProgress = 1.
// Visible in editor and in XR (fades from a solid color to clear).
// For true passthrough: set Fade Color to black (alpha handled by XR compositor).
Shader "Custom/FadeOverlay"
{
    Properties
    {
        _FadeColor ("Fade Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+500" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Cull     Front   // camera is inside the sphere
        ZWrite   Off
        ZTest    Always  // draw over everything
        Blend    SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _FadeColor;
            float  _FadeProgress;   // set globally by SceneFadeBroadcaster / FadeProgressOverride

            struct Attributes { float4 posOS : POSITION; };
            struct Varyings   { float4 posCS : SV_POSITION; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.posCS = TransformObjectToHClip(i.posOS.xyz);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // progress=0 → alpha=1 (solid colour, covers everything)
                // progress=1 → alpha=0 (fully transparent, scene visible)
                return half4(_FadeColor.rgb, 1.0 - _FadeProgress);
            }
            ENDHLSL
        }
    }
}
