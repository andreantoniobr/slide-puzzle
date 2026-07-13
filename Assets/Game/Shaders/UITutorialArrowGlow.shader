Shader "Custom/UITutorialArrowGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture (branco)", 2D) = "white" {}

        [Header(Cor Base)]
        _BaseColor ("Cor na base da seta", Color) = (1, 0.6, 0.1, 1)
        _TipColor  ("Cor na ponta da seta", Color) = (1, 0.95, 0.4, 1)

        [Header(Glow Pulsante)]
        _GlowColor ("Cor do brilho", Color) = (1, 0.85, 0.3, 1)
        _GlowSize ("Tamanho do brilho (UV)", Range(0.0, 0.15)) = 0.05
        _GlowIntensity ("Intensidade do brilho", Range(0, 3)) = 1.2
        _PulseSpeed ("Velocidade do pulso", Float) = 3.0
        _PulseAmount ("Quanto o pulso varia", Range(0, 1)) = 0.4

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
        Blend SrcAlpha One // aditivo — reforça o efeito de "luz"
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
            float4 _MainTex_TexelSize;

            fixed4 _BaseColor;
            fixed4 _TipColor;

            fixed4 _GlowColor;
            float _GlowSize;
            float _GlowIntensity;
            float _PulseSpeed;
            float _PulseAmount;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color;
                return o;
            }

            // Blur simples de 8 amostras, usado só para gerar o glow ao redor da forma
            float SampleGlowAlpha(float2 uv, float spread)
            {
                float total = 0;
                total += tex2D(_MainTex, uv + float2( spread,  0)).a;
                total += tex2D(_MainTex, uv + float2(-spread,  0)).a;
                total += tex2D(_MainTex, uv + float2( 0,  spread)).a;
                total += tex2D(_MainTex, uv + float2( 0, -spread)).a;
                total += tex2D(_MainTex, uv + float2( spread,  spread)).a;
                total += tex2D(_MainTex, uv + float2(-spread,  spread)).a;
                total += tex2D(_MainTex, uv + float2( spread, -spread)).a;
                total += tex2D(_MainTex, uv + float2(-spread, -spread)).a;
                return saturate(total / 8.0);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.texcoord);

                // Gradiente de cor da base (y=0) até a ponta (y=1) — dá a sensação de "chama"
                fixed4 gradientColor = lerp(_BaseColor, _TipColor, i.texcoord.y);

                fixed4 core = tex * gradientColor * i.color;

                // Pulso: varia a intensidade do glow ao longo do tempo
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                // Glow ao redor da forma, usando o blur de 8 amostras
                float glowAlpha = SampleGlowAlpha(i.texcoord, _GlowSize);
                fixed4 glow = _GlowColor * glowAlpha * _GlowIntensity * pulse;

                fixed4 result = core + glow * (1.0 - tex.a); // glow só aparece FORA do sprite sólido
                result.a = saturate(tex.a + glowAlpha * _GlowIntensity * 0.5);

                return result;
            }
            ENDCG
        }
    }
}