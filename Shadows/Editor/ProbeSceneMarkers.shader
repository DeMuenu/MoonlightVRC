Shader "Hidden/MoonlightVRC/ProbeSceneMarkers"
{
    SubShader
    {
        Tags { "Queue" = "Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _MarkerRadius;
            float4 _ViewportSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.vertex.xy += v.uv * (2.0 * _MarkerRadius / _ViewportSize.xy) * o.vertex.w;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                clip(1.0 - dot(i.uv, i.uv));
                return i.color;
            }
            ENDCG
        }
    }
}
