Shader "DeMuenu/World/Hoppou/Particles/LitParticles"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        
        //Moonlight
        _InverseSqareMultiplier ("Inverse Square Multiplier", Float) = 1
        _LightCutoffDistance ("Light Cutoff Distance", Float) = 100


        _shadowCasterTex ("Shadow Caster Texture", 2D) = "white" {}
        _shadowCasterColor ("Shadow Caster Color", Color) = (1,1,1,1)
        _OutSideColor ("Outside Color", Color) = (1,1,1,1)
        _MinBrightnessShadow ("Min Brightness for Shadows", Range(0,1)) = 0
        //Moonlight END



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
            #include "Includes/Variables.hlsl"
            #include "Includes/Shadowcaster.cginc"



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
                //Moonlight
                float3 worldPos : TEXCOORD2;

                float4 color : COLOR;

                float3 worldNormal: TEXCOORD3;
                //Moonlight END
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


            MoonlightGlobalVariables


            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);


                //Moonlight
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = wp.xyz;
                o.color = v.color;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                //Moonlight END
                

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);


                //Moonlight
                float3 N = normalize(i.worldNormal); /*for lambertian diffuse*/

                OutLoopSetup(i, _Udon_PlayerCount) //defines count, N, dmax, dIntensity
                
                [loop]
                for (int LightCounter = 0; LightCounter < MAX_LIGHTS; LightCounter++)
                {

                    InLoopSetup(_Udon_LightPositions, LightCounter, count, i); //defines distanceFromLight, contrib

                    Lambert(_Udon_LightPositions[LightCounter].xyz ,i, N); //defines NdotL

                    LightTypeCalculations(_Udon_LightColors, LightCounter, i, NdotL, dIntensity, _Udon_LightPositions[LightCounter].a, _Udon_LightPositions[LightCounter].xyz);

                    float4 ShadowCasterMult = float4(1,1,1,1);
                    if (_Udon_ShadowMapIndex[LightCounter] > 0.5) {
                    ShadowCasterMult = SampleShadowcasterPlane(_Udon_WorldToLocal, _shadowCasterTex, _Udon_LightPositions[LightCounter].xyz, i.worldPos, _OutSideColor);
                    ShadowCasterMult *= _shadowCasterColor;
                    ShadowCasterMult = float4(ShadowCasterMult.rgb * (1-ShadowCasterMult.a), 1);
                    }

                    dmax = dmax + contrib * float4(LightColor, 1) * 1 * max(ShadowCasterMult, _MinBrightnessShadow);

                }
                
                //dmax.xyz = min(dmax * dIntensity, 1.0);
                dmax.w = 1.0;

                //Moonlight END

                return col * _Color * min(dmax, 1.0) * i.color;

            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}