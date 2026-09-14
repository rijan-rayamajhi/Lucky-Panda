Shader "UI/FrameSDF"
{
    // Rounded-rect UI frame drawn from vertex data (no texture).
    // Per-instance params are packed by ThemedFrame.cs into UV channels so a
    // single shared material works for every frame and still batches.
    //   uv0 = (nx, ny, width_px, height_px)
    //   uv1 = (cornerRadius_px, borderThickness_px, _, _)
    //   uv2 = border color (rgba)
    //   color = fill color (gradient via top/bottom verts)
    Properties
    {
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float4 uv0    : TEXCOORD0;
                float4 uv1    : TEXCOORD1;
                float4 uv2    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 fill          : COLOR;
                float4 uv0           : TEXCOORD0;
                float4 uv1           : TEXCOORD1;
                float4 uv2           : TEXCOORD2;
                float4 worldPosition : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.fill = v.color * _Color;
                o.uv0 = v.uv0;
                o.uv1 = v.uv1;
                o.uv2 = v.uv2;
                return o;
            }

            // Signed distance to a rounded box centred at origin.
            float sdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float w = i.uv0.z;
                float h = i.uv0.w;
                float2 ext = float2(w, h) * 0.5;
                float2 p = float2((i.uv0.x - 0.5) * w, (i.uv0.y - 0.5) * h);

                float radius = min(i.uv1.x, min(ext.x, ext.y));
                float border = max(i.uv1.y, 0.0);

                float d  = sdRoundBox(p, ext, radius);
                float aa = max(fwidth(d), 1e-4);

                float shape    = 1.0 - smoothstep(-aa, aa, d);              // whole rounded rect
                float fillMask = 1.0 - smoothstep(-border - aa, -border + aa, d); // interior inside border

                float4 bord  = i.uv2;
                float3 rgb   = lerp(bord.rgb, i.fill.rgb, fillMask);
                float  alpha = lerp(bord.a,   i.fill.a,   fillMask) * shape;

                fixed4 col = fixed4(rgb, alpha);

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
