Shader "Custom/UIWoodFrameBevel"
{
    Properties
    {
        _MainTex ("Wood Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _BevelHighlight ("Highlight Color", Color) = (1,1,1,1)
        _BevelShadow ("Shadow Color", Color) = (0,0,0,1)
        _BevelStrength ("Bevel Strength", Range(0,1)) = 0.5
        _HighlightBoost ("Highlight Boost", Range(0,1)) = 0.3
        _ShadowDarken ("Shadow Darken", Range(0,1)) = 0.4

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
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            fixed4 _BevelHighlight;
            fixed4 _BevelShadow;
            float _BevelStrength;
            float _HighlightBoost;
            float _ShadowDarken;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                // Respeita totalmente o UV gerado pelo seu pipeline C# (UVGenerator)
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Amostra a textura usando diretamente o UV do pipeline
                fixed4 tex = tex2D(_MainTex, i.texcoord);
                fixed4 col = tex * i.color;

                // Canal V define a largura (chanfro interno/externo)
                float v = saturate(i.texcoord.y);
                float ridge = sin(v * 3.14159265); 

                // Aplica o sombreamento e relevo do bevel
                float shadowFactor = 1.0 - ((1.0 - ridge) * _ShadowDarken * _BevelStrength);
                float highlightFactor = 1.0 + (ridge * _HighlightBoost * _BevelStrength);
                col.rgb *= shadowFactor * highlightFactor;

                fixed3 tinted = lerp(_BevelShadow.rgb, _BevelHighlight.rgb, ridge);
                col.rgb = lerp(col.rgb, col.rgb * tinted, _BevelStrength);

                return col;
            }
            ENDCG
        }
    }
}