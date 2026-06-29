Shader "UI/Vignette"
{
    Properties
    {
        _Intensity  ("Intensity",  Range(0, 5)) = 2.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Color      ("Vignette Color", Color)   = (0,0,0,1)
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

            float  _Intensity;
            float  _Smoothness;
            float4 _Color;

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
                float  dist = length(uv) * _Intensity;

                // Centro = 0 (transparente), borda = 1 (opaco)
                float inner = 0.5 - _Smoothness * 0.5;
                float outer = 0.5 + _Smoothness * 0.5;
                float vignette = smoothstep(inner, outer, dist);

                return fixed4(_Color.rgb, _Color.a * vignette);
            }
            ENDCG
        }
    }
}
