Shader "UI/Sunburst"
{
    Properties
    {
        // ── Raios ────────────────────────────────────────────────────
        _Arms           ("Arms (qtd raios)",     Range(2, 64))   = 16
        _ArmSharpness   ("Arm Sharpness",        Range(1, 200))  = 80.0
        // Sharpness: 1 = muito difuso/gradiente  |  200 = corte duro entre raios

        _ArmRatio       ("Arm Ratio (claro/esc)",Range(0.01,0.99))= 0.5
        // 0.5 = raios iguais  |  0.2 = raios finos  |  0.8 = raios largos

        // ── Fade radial ──────────────────────────────────────────────
        _InnerRadius    ("Inner Radius",         Range(0, 0.5))  = 0.0
        _InnerBlur      ("Inner Blur",           Range(0, 0.3))  = 0.05

        _OuterRadius    ("Outer Radius",         Range(0.1, 1.0))= 0.5
        _OuterBlur      ("Outer Blur",           Range(0, 0.4))  = 0.0

        // ── Rotação ──────────────────────────────────────────────────
        _Speed          ("Speed",                Range(-10, 10)) = 0.5
        _AngleOffset    ("Angle Offset (graus)",  Range(0, 360))  = 0.0

        // ── Cores ────────────────────────────────────────────────────
        _ColorA         ("Color A (raio claro)", Color)          = (1,1,1,1)
        _ColorB         ("Color B (raio escuro)",Color)          = (0.85,0.85,0.85,0)
        // ColorB alpha 0 = raio escuro completamente transparente (só raios claros visíveis)
        // ColorB alpha 1 = alterna entre duas cores sólidas

        // ── Intensidade global ───────────────────────────────────────
        _Intensity      ("Intensity",            Range(0, 1))    = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float  _Arms;
            float  _ArmSharpness;
            float  _ArmRatio;
            float  _InnerRadius;
            float  _InnerBlur;
            float  _OuterRadius;
            float  _OuterBlur;
            float  _Speed;
            float  _AngleOffset;
            float4 _ColorA;
            float4 _ColorB;
            float  _Intensity;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f    { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv   = i.uv - 0.5;
                float  dist = length(uv);

                // ── Ângulo com rotação e offset ──────────────────────
                float offsetRad = _AngleOffset * (3.14159265 / 180.0);
                float angle     = atan2(uv.y, uv.x)
                                + _Time.y * _Speed
                                + offsetRad;

                // ── Padrão de raios ──────────────────────────────────
                // Normaliza o ângulo para 0..1 por braço
                float t = frac(angle / (3.14159265 * 2.0) * _Arms);

                // smoothstep com ArmRatio define onde termina o raio claro
                // ArmSharpness controla quão duro é o corte
                float halfBlur = 1.0 / max(_ArmSharpness, 1.0);
                float ray = smoothstep(_ArmRatio - halfBlur, _ArmRatio + halfBlur, t)
                          * smoothstep(1.0 - halfBlur, 1.0 - halfBlur * 2.0, t);
                // ray = 0 → raio claro (ColorA)  |  ray = 1 → raio escuro (ColorB)
                // invertemos abaixo pra facilitar a leitura
                ray = 1.0 - ray;

                // ── Máscara radial ───────────────────────────────────
                float innerEdge0 = _InnerRadius;
                float innerEdge1 = _InnerRadius + max(_InnerBlur, 0.001);
                float innerMask  = smoothstep(innerEdge0, innerEdge1, dist);

                float outerEdge0 = _OuterRadius - max(_OuterBlur, 0.001);
                float outerEdge1 = _OuterRadius;
                float outerMask  = 1.0 - smoothstep(outerEdge0, outerEdge1, dist);

                float radialMask = innerMask * outerMask;

                // ── Mistura entre ColorA e ColorB ────────────────────
                float4 col   = lerp(_ColorA, _ColorB, 1.0 - ray);
                float  alpha = col.a * radialMask * _Intensity;

                return fixed4(col.rgb, saturate(alpha));
            }
            ENDCG
        }
    }
}
