Shader "DeMuenu/World/Hoppou/Particles/LitParticles"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        
        //MoonsLight
        _InverseSqareMultiplier ("Inverse Square Multiplier", Float) = 1
        _LightCutoffDistance ("Light Cutoff Distance", Float) = 100
        //MoonsLight END



    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend SrcAlpha One
        Cull Off
        Lighting Off
        ZWrite Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #define MAX_LIGHTS 80 // >= maxPlayers in script

            #include "UnityCG.cginc"
            #include "Includes/LightStrength.hlsl"
            #include "Includes/Lambert.hlsl"
            #include "Includes/DefaultSetup.hlsl"



            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;

                float4 color  : COLOR;

                float3 normal : NORMAL;

            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                //MoonsLight
                float3 worldPos : TEXCOORD2;

                float4 color : COLOR;

                float3 worldNormal: TEXCOORD3;
                //MoonsLight END
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _MultTex;
            float4 _MultTex_ST;
            float4 _Color;


            sampler2D _EmmisiveText;
            float4 _EmmisiveText_ST;
            float4 _EmmissiveColor;
            float _EmmissiveStrength;


            //MoonsLight
            float _InverseSqareMultiplier;
            float _LightCutoffDistance;            
            
            float4 _LightPositions[MAX_LIGHTS]; // xyz = position
            float4 _LightColors[MAX_LIGHTS]; // xyz = position
            float4 _LightDirections[MAX_LIGHTS]; // xyz = direction, w = cos(halfAngle)
            float _LightType[MAX_LIGHTS]; // 0 = sphere, 1 = cone
            float  _PlayerCount;                  // set via SetFloat
            //MoonsLight END


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);


                //MoonsLight
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = wp.xyz;
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                //MoonsLight END
                

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);


                //MoonsLight
                int count = (int)_PlayerCount;

                float3 N = normalize(i.worldNormal); //for lambertian diffuse
                // Example: compute distance to nearest player
                float4 dmax = float4(0,0,0,1);
                float dIntensity = 0;
                [loop]
                for (int LightCounter = 0; LightCounter < MAX_LIGHTS; LightCounter++)
                {

                    InLoopSetup(_LightPositions, LightCounter, count, i); //defines distanceFromLight, contrib

                    Lambert(_LightPositions[LightCounter].xyz ,i, N); //defines NdotL

                    LightTypeCalculations(_LightColors, LightCounter, i, NdotL, dIntensity, _LightPositions[LightCounter].a, _LightPositions[LightCounter].xyz);




                    dmax = dmax + contrib * float4(LightColor, 1) * NdotL; // accumulate light contributions

                    


                }
                
                //dmax.xyz = min(dmax * dIntensity, 1.0);
                dmax.w = 1.0;
                dmax = dmax;


                //MoonsLight END



                return col * _Color * min(dmax, 1.0) * i.color;

            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}