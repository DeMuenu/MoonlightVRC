Shader "DeMuenu/World/Hoppou/Particles/LitParticles"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)

        
        //Moonlight
        _InverseSqareMultiplier ("Inverse Square Multiplier", Float) = 1
        _LightCutoffDistance ("Light Cutoff Distance", Float) = 100
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

            float4 _Udon_Plane_Origin_1;   // xyz = origin (world), w unused
            float4 _Udon_Plane_Uinv_1;     // xyz = Udir / (2*halfWidth)
            float4 _Udon_Plane_Vinv_1;     // xyz = Vdir / (2*halfHeight)
            float4 _Udon_Plane_Normal_1;   // xyz = unit normal

            sampler2D _Udon_shadowCasterTex_1;
            float4 _Udon_shadowCasterColor_1;
            float4 _Udon_OutSideColor_1;
            float _Udon_MinBrightnessShadow_1;

            float4 _Udon_Plane_Origin_2;
            float4 _Udon_Plane_Uinv_2;
            float4 _Udon_Plane_Vinv_2;
            float4 _Udon_Plane_Normal_2;

            sampler2D _Udon_shadowCasterTex_2;
            float4 _Udon_shadowCasterColor_2;
            float4 _Udon_OutSideColor_2;
            float _Udon_MinBrightnessShadow_2;
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


                    LightTypeCalculations(_Udon_LightColors, LightCounter, i, 1, dIntensity, _Udon_LightPositions[LightCounter].a, _Udon_LightPositions[LightCounter].xyz);

                    float4 ShadowCasterMult_1 = 1;
                    float4 ShadowCasterMult_2 = 1;

                    if (((_Udon_ShadowMapIndex[LightCounter] > 0.5) && (_Udon_ShadowMapIndex[LightCounter] < 1.5)) || (_Udon_ShadowMapIndex[LightCounter] > 2.5))
                    {
                        float4 sc1 = SampleShadowcasterPlaneWS_Basis(
                            _Udon_LightPositions[LightCounter].xyz, i.worldPos,
                            _Udon_Plane_Origin_1.xyz, _Udon_Plane_Uinv_1.xyz, _Udon_Plane_Vinv_1.xyz, _Udon_Plane_Normal_1.xyz,
                            _Udon_shadowCasterTex_1, _Udon_OutSideColor_1, _Udon_shadowCasterColor_1);
                        ShadowCasterMult_1 = max(sc1, _Udon_MinBrightnessShadow_1);
                    }

                    if (_Udon_ShadowMapIndex[LightCounter] > 1.5)
                    {
                        float4 sc2 = SampleShadowcasterPlaneWS_Basis(
                            _Udon_LightPositions[LightCounter].xyz, i.worldPos,
                            _Udon_Plane_Origin_2.xyz, _Udon_Plane_Uinv_2.xyz, _Udon_Plane_Vinv_2.xyz, _Udon_Plane_Normal_2.xyz,
                            _Udon_shadowCasterTex_2, _Udon_OutSideColor_2, _Udon_shadowCasterColor_2);
                        ShadowCasterMult_2 = max(sc2, _Udon_MinBrightnessShadow_2);
                    }

                    dmax = dmax + contrib * float4(LightColor, 1) * ShadowCasterMult_1 * ShadowCasterMult_2;

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