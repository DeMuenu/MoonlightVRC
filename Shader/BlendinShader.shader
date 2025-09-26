Shader "DeMuenu/World/Hoppou/RevealStandart"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MultTex ("Multiply Texture", 2D) = "white" {}
        _MultiplicatorTex ("Multiply Texture Strength", Range(0,3)) = 0
        _Color ("Color", Color) = (1,1,1,1)

        _EmmisiveText ("Emmissive Texture", 2D) = "white" {}
        _EmmissiveColor ("Emmissive Color", Color) = (1,1,1,1)
        _EmmissiveStrength ("Emmissive Strength", Range(0,10)) = 0

        
        //Moonlight
        _InverseSqareMultiplier ("Inverse Square Multiplier", Float) = 1
        _LightCutoffDistance ("Light Cutoff Distance", Float) = 100
        //Moonlight END



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

            #include "UnityCG.cginc"
            #include "Includes/LightStrength.hlsl"
            #include "Includes/Lambert.hlsl"
            #include "Includes/DefaultSetup.hlsl"
            #include "Includes/Variables.hlsl"

            //Moonlight Defines
            #define MAX_LIGHTS 80 // >= maxPlayers in script
            //Moonlight Defines END

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float2 uvEmmis : TEXCOORD4;
                //UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;

                //Moonlight
                float3 worldPos : TEXCOORD2;
                float3 worldNormal: TEXCOORD3;

                //Moonlight END

                
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MultTex;
            float4 _MultTex_ST;
            float _MultiplicatorTex;
            float4 _Color;


            sampler2D _EmmisiveText;
            float4 _EmmisiveText_ST;
            float4 _EmmissiveColor;
            float _EmmissiveStrength;


            MoonlightGlobalVariables


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv2 = TRANSFORM_TEX(v.uv, _MultTex);
                o.uvEmmis = TRANSFORM_TEX(v.uv, _EmmisiveText);


                //Moonlight Vertex
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = wp.xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                //Moonlight Vertex END
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 mult = tex2D(_MultTex, i.uv2);
                col = lerp(col,  mult, _MultiplicatorTex);
                fixed4 emmis = tex2D(_EmmisiveText, i.uvEmmis);


                //Moonlight
                float3 N = normalize(i.worldNormal); /*for lambertian diffuse*/

                OutLoopSetup(i, _Udon_PlayerCount) //defines count, N, dmax, dIntensity

                [loop]
                for (int LightCounter = 0; LightCounter < MAX_LIGHTS; LightCounter++)
                {
                    InLoopSetup(_Udon_LightPositions, LightCounter, count, i); //defines distanceFromLight, contrib

                    
                    //Lambertian diffuse
                    Lambert(_Udon_LightPositions[LightCounter].xyz ,i, N); //defines NdotL

                    LightTypeCalculations(_Udon_LightColors, LightCounter, i, NdotL, dIntensity, _Udon_LightPositions[LightCounter].a, _Udon_LightPositions[LightCounter].xyz);

                    dmax = dmax + contrib * float4(LightColor, 1) * NdotL; // accumulate light contributions

                }
                
                //dmax.xyz = min(dmax * dIntensity, 1.0);
                dmax.w = 1.0;

                //Moonlight END

                return col * _Color * dmax + emmis * _EmmissiveStrength * _EmmissiveColor;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}