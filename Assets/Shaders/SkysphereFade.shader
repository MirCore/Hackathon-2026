Shader "Custom/SkysphereFade"
{
    Properties
    {
        _MainTex      ("Texture",       2D)            = "white" {}
        _Color        ("Color Tint",    Color)         = (1,1,1,1)
        _EdgeSoftness ("Edge Softness", Range(0.001,0.5)) = 0.08
        // Axis along which the reveal sweeps (in sphere local space)
        // 0 = +Y (bottom→top), 1 = −Z (back→front), 2 = +Z (front→back), 3 = −Y (top→bottom)
        [KeywordEnum(Up, Forward, Back, Down)] _FadeDir ("Reveal Direction", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent+1"
            "RenderType"     = "Transparent"
            "IgnoreProjector"= "True"
        }

        Cull  Front   // render inside of sphere
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float  slideValue : TEXCOORD1;  // 0 = first revealed, 1 = last revealed
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _Color;
            float     _FadeProgress;  // set globally by SceneFadeBroadcaster
            float     _EdgeSoftness;
            float     _FadeDir;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);

                // normalised direction on the sphere surface (local space)
                float3 dir = normalize(v.vertex.xyz);

                // pick the axis component in [-1,1], then remap to [0,1]
                float axisVal;
                if      (_FadeDir < 0.5) axisVal =  dir.y;   // Up:      bottom first
                else if (_FadeDir < 1.5) axisVal = -dir.z;   // Forward: back first
                else if (_FadeDir < 2.5) axisVal =  dir.z;   // Back:    front first
                else                     axisVal = -dir.y;   // Down:    top first

                o.slideValue = axisVal * 0.5 + 0.5;          // [0,1]
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;

                // smoothstep returns 0 below (progress-edge), 1 above (progress+edge)
                // invert so the already-swept region is opaque
                float progress = _FadeProgress * 1.2 - 0.1;
                float alpha = 1.0 - smoothstep(
                    progress - _EdgeSoftness,
                    progress + _EdgeSoftness,
                    i.slideValue
                );

                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}
