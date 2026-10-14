Shader "DeMuenu/MoonlightVRC/backend/shadowCalculator"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OtherTex ("Another Texture to Sample", 2D) = "white" {}
        _CustomParam ("Function Parameter", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        ZWrite Off 
        ZTest Always
        Cull Off // Prevents the quad from disappearing when viewed from certain angles

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _OtherTex;
            float _CustomParam;

            v2f vert (appdata v)
            {
                v2f o;
                // Map standard quad local space (-0.5 to 0.5) directly to NDC screen space (-1 to 1)
                o.vertex = float4(v.vertex.xy * 2.0, 0.0, 1.0);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                int2 pixelCoord = int2(i.uv * _MainTex_TexelSize.zw);
                bool checker = (pixelCoord.x / 16 + pixelCoord.y / 16) % 2 == 0;

                float2 targetUV = float2(0.5, 0.5);
                fixed4 otherTexPixel = tex2D(_OtherTex, targetUV);

                fixed4 baseCol = tex2D(_MainTex, i.uv);
                fixed4 finalCol = checker ? baseCol : otherTexPixel;

                return finalCol;
            }
            ENDCG
        }
    }
}