Shader "DeMuenu/World/Hoppou/GhostWhite"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _EmmissiveColor ("Emmissive Color", Color) = (1,1,1,1)
        _EmmissiveStrength ("Emmissive Strength", Range(0,10)) = 0

        _BaseColor    ("Base Color", Color) = (0.06,0.08,0.1,1)
        _FresnelColor ("Fresnel Color", Color) = (0.3,0.7,1,1)
        _Power        ("Fresnel Power", Range(0.1, 8)) = 3
        _Intensity    ("Fresnel Intensity", Range(0, 4)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal  : TEXCOORD2;
                float3 worldViewDir : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _EmmissiveColor;
            float _EmmissiveStrength;
            fixed4 _BaseColor, _FresnelColor;
            float  _Power, _Intensity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal  = UnityObjectToWorldNormal(v.normal);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldViewDir = _WorldSpaceCameraPos - worldPos;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.worldNormal);
                float3 V = normalize(i.worldViewDir);
                // Schlick-style rim: (1 - N·V)^power
                float fresnel = pow(1.0 - saturate(dot(N, V)), _Power);

                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv) * _Color ;


                col = float4(col.rgb + _FresnelColor.rgb * (fresnel * _Intensity), 1);
                
                // apply fog
                return col + (_EmmissiveColor * _EmmissiveStrength);
            }
            ENDCG
        }
    }
}
