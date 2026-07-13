Shader "Custom/UITileCorrectGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // ── Shine de posição correta (disparo único) ──
        _ShineColor ("Shine Color", Color) = (1, 1, 1, 0.8)
        _ShineWidth ("Shine Width", Range(0.01, 0.5)) = 0.15
        _ShineDuration ("Shine Duration", Float) = 0.6
        _CorrectTimestamp ("Correct Timestamp", Float) = -1000
        _IsCorrect ("Is Correct (0/1)", Range(0,1)) = 0

        // ── Glow de borda rotativo (peça selecionada, aguardando escolha) ──
        _SelectionGlowColor ("Selection Glow Color", Color) = (0.3, 0.8, 1, 1)
        _SelectionBorderWidth ("Selection Border Width", Range(0.01, 0.3)) = 0.06
        _SelectionBandWidth ("Selection Band Width", Range(0.05, 1.0)) = 0.25
        _SelectionRotationSpeed ("Selection Rotation Speed", Float) = 0.8
        _IsSelected ("Is Selected (0/1)", Range(0,1)) = 0

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

            #define PI 3.14159265359

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
            fixed4 _Color;

            fixed4 _ShineColor;
            float _ShineWidth;
            float _ShineDuration;
            float _CorrectTimestamp;
            float _IsCorrect;

            fixed4 _SelectionGlowColor;
            float _SelectionBorderWidth;
            float _SelectionBandWidth;
            float _SelectionRotationSpeed;
            float _IsSelected;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.texcoord);
                fixed4 col = texColor * i.color;

                // ── Shine de posição correta (uma vez) ──
                if (_IsCorrect > 0.5)
                {
                    float elapsed = _Time.y - _CorrectTimestamp;
                    if (elapsed >= 0.0 && elapsed < _ShineDuration)
                    {
                        float sweepT = elapsed / _ShineDuration;
                        float diag = (i.texcoord.x + i.texcoord.y) * 0.5;
                        float sweepPos = lerp(-_ShineWidth, 1.0 + _ShineWidth, sweepT);
                        float dist = abs(diag - sweepPos);
                        float shine = smoothstep(_ShineWidth, 0.0, dist);
                        col.rgb += _ShineColor.rgb * shine * _ShineColor.a;
                    }
                }

                // ── Glow de borda rotativo (peça selecionada) ──
                if (_IsSelected > 0.5)
                {
                    float edgeDist = min(min(i.texcoord.x, 1.0 - i.texcoord.x),
                                          min(i.texcoord.y, 1.0 - i.texcoord.y));
                    float borderMask = smoothstep(_SelectionBorderWidth, 0.0, edgeDist);

                    float2 centered = i.texcoord - 0.5;
                    float angle = atan2(centered.y, centered.x) / (2.0 * PI) + 0.5; // 0 a 1

                    float rotation = frac(_Time.y * _SelectionRotationSpeed);
                    float angDelta = abs(frac(angle - rotation + 0.5) - 0.5); // distância angular com wrap-around
                    float band = smoothstep(_SelectionBandWidth * 0.5, 0.0, angDelta);

                    col.rgb += _SelectionGlowColor.rgb * borderMask * band * _SelectionGlowColor.a;
                }

                return col;
            }
            ENDCG
        }
    }
}