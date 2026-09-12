Shader "DeMuenu/MoonlightVRC/Standard_2SP_Metallic"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalMapStrength ("Normal Map Strength", Range(0,1)) = 1
        _Color ("Color", Color) = (1,1,1,1)

        _EmmisiveText ("Emmissive Texture", 2D) = "white" {}
        _EmmissiveColor ("Emmissive Color", Color) = (1,1,1,1)
        _EmmissiveStrength ("Emmissive Strength", Range(0,10)) = 0

        _MetallicTex ("Metallic Texture", 2D) = "white" {}

        _MetallicMult ("Metallic Multiplier", Range(0,1)) = 0
        
        _F0 ("F0", Range(0,1)) = 0.02
        _FresnelPower ("Fresnel Power", Range(1,8)) = 5
        _ReflectionStrength ("Reflection Strength", Range(0,1)) = 0.7
        

        //Moonlight
        _InverseSqareMultiplier ("Inverse Square Multiplier", Float) = 1
        _LightCutoffDistance ("Light Cutoff Distance", Float) = 100

        _EnableShadowCasting ("Enable Shadowcasting", Float) = 0
        _BlurPixels ("Shadowcaster Blur Pixels", Float) = 0
        //Moonlight END

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2



    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        Cull[_Cull]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            //Moonlight Defines
            #define MAX_LIGHTS 80 // >= maxPlayers in script
            //Moonlight Defines END
            
            #include "UnityCG.cginc"
            #include "Includes/Moonlight.hlsl"



            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float2 uvEmmis : TEXCOORD4;
                float4 vertex : SV_POSITION;
                float2 normUV : TEXCOORD5;
                float3 worldTangent   : TEXCOORD6;
                float3 worldBitangent : TEXCOORD7;

                //Moonlight
                float3 worldPos : TEXCOORD2;
                float3 worldNormal: TEXCOORD3;
                //Moonlight END

                
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NormalMap;
            float4 _NormalMap_ST;
            float4 _Color;
            float _NormalMapStrength;
            


            sampler2D _EmmisiveText;
            float4 _EmmisiveText_ST;
            float4 _EmmissiveColor;
            float _EmmissiveStrength;


            sampler2D _MetallicTex;
            float _MetallicMult;
            
            float _F0, _FresnelPower, _ReflectionStrength;

            inline float SchlickFresnel(float NoV, float F0, float power)
            {
                float f = pow(saturate(1.0 - NoV), power);
                return saturate(F0 + (1.0 - F0) * f);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normUV = TRANSFORM_TEX(v.uv, _NormalMap);
                o.uvEmmis = TRANSFORM_TEX(v.uv, _EmmisiveText);

                float3 nWS = UnityObjectToWorldNormal(v.normal);
                float3 tWS = normalize(UnityObjectToWorldDir(v.tangent.xyz));
                float3 bWS = normalize(cross(nWS, tWS) * v.tangent.w);

                o.worldNormal   = nWS;
                o.worldTangent  = tWS;
                o.worldBitangent= bWS;


                //Moonlight Vertex
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = wp.xyz;
                //o.worldNormal = UnityObjectToWorldNormal(v.normal);
                //Moonlight Vertex END
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 norm = tex2D(_NormalMap, i.normUV);

                fixed4 emmis = tex2D(_EmmisiveText, i.uvEmmis);


                //Moonlight
                float3 nTS = UnpackNormal(norm);
                float3 NmapWS = normalize(i.worldTangent * nTS.x +
                                        i.worldBitangent * nTS.y +
                                        i.worldNormal   * nTS.z);
                float3 N = normalize(lerp(normalize(i.worldNormal), NmapWS, saturate(_NormalMapStrength)));


                float metallic = tex2D(_MetallicTex, i.uv).r * _MetallicMult;
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 R = reflect(-V, N);  //for reflection vector
                float  NoV = saturate(dot(N, V));
                float fresnelTerm = SchlickFresnel(NoV, _F0, _FresnelPower);
                float specularStrength = lerp(_F0, 1.0, metallic) * _ReflectionStrength;
                float3 specularAccum = 0;

                OutLoopSetup(i, _Udon_PlayerCount) //defines count, N, dmax, dIntensity

                [loop]
                for (int LightCounter = 0; LightCounter < count; LightCounter++)
                {
                    InLoopSetup(_Udon_LightPositions, LightCounter, count, i); //defines distanceFromLight, contrib

                    
                    //Lambertian diffuse
                    Lambert(_Udon_LightPositions[LightCounter].xyz ,i, N); //defines NdotL

                    // --- Specular (Blinn-Phong) ---
                    //float3 L = normalize(_Udon_LightPositions[LightCounter].xyz - i.worldPos);
                    float3 H = normalize(V + L);
                    float NdotH = saturate(dot(N, H));
                    float NdotL_spec = saturate(dot(N, L));

                    // Gloss from metallic map: more metallic = sharper highlight
                    // Adjust the multiplier (64.0) to taste
                    float gloss = lerp(8.0, 256.0, metallic);
                    float blinnPhong = pow(NdotH, gloss) * NdotL_spec;

                    // Fresnel per-light (based on VdotH for accuracy)
                    float VdotH = saturate(dot(V, H));
                    float F = SchlickFresnel(VdotH, _F0, _FresnelPower);

                    

                    LightTypeCalculations(_Udon_LightColors, LightCounter, i, NdotL, dIntensity, _Udon_LightPositions[LightCounter].a, _Udon_LightPositions[LightCounter].xyz);
                    
                    float4 ShadowCasterMult_1 = 1;
                    float4 ShadowCasterMult_2 = 1;

                    if ((((_Udon_ShadowMapIndex[LightCounter] > 0.5) && (_Udon_ShadowMapIndex[LightCounter] < 1.5) && (_EnableShadowCasting > 0.5)) || (_Udon_ShadowMapIndex[LightCounter] > 2.5)) && _EnableShadowCasting)                    {
                        float4 sc1 = SampleShadowcasterPlaneWS_Basis(
                            _Udon_LightPositions[LightCounter].xyz, i.worldPos,
                            _Udon_Plane_Origin_1.xyz, _Udon_Plane_Uinv_1.xyz, _Udon_Plane_Vinv_1.xyz, _Udon_Plane_Normal_1.xyz,
                            _Udon_shadowCasterTex_1, _Udon_OutSideColor_1, _Udon_shadowCasterColor_1, _BlurPixels, _Udon_shadowCasterTex_1_TexelSize.xy);
                        ShadowCasterMult_1 = max(sc1, _Udon_MinBrightnessShadow_1);
                    }
                    if (_Udon_ShadowMapIndex[LightCounter] > 1.5 && (_EnableShadowCasting > 0.5))
                    {
                        half smIndex = _Udon_ShadowMapIndex[LightCounter];
                        if ((smIndex > 0.5 && smIndex < 1.5) || smIndex > 2.5)
                        {
                            float4 sc1 = SampleShadowcasterPlaneWS_Basis(
                                _Udon_LightPositions[LightCounter].xyz, i.worldPos,
                                _Udon_Plane_Origin_1.xyz, _Udon_Plane_Uinv_1.xyz, _Udon_Plane_Vinv_1.xyz, _Udon_Plane_Normal_1.xyz,
                                _Udon_shadowCasterTex_1, _Udon_OutSideColor_1, _Udon_shadowCasterColor_1, _BlurPixels, _Udon_shadowCasterTex_1_TexelSize.xy);
                            ShadowCasterMult_1 = max(sc1, _Udon_MinBrightnessShadow_1);
                        }
                        if (smIndex > 1.5)
                        {
                            float4 sc2 = SampleShadowcasterPlaneWS_Basis(
                                _Udon_LightPositions[LightCounter].xyz, i.worldPos,
                                _Udon_Plane_Origin_2.xyz, _Udon_Plane_Uinv_2.xyz, _Udon_Plane_Vinv_2.xyz, _Udon_Plane_Normal_2.xyz,
                                _Udon_shadowCasterTex_2, _Udon_OutSideColor_2, _Udon_shadowCasterColor_2, _BlurPixels, _Udon_shadowCasterTex_2_TexelSize.xy);
                            ShadowCasterMult_2 = max(sc2, _Udon_MinBrightnessShadow_2);
                        }
                    }

                    dmax = dmax + contrib * float4(LightColor, 1) * NdotL * ShadowCasterMult_1 * ShadowCasterMult_2;

                    specularAccum += LightColor * contrib * blinnPhong * F * specularStrength * ShadowCasterMult_1.rgb * ShadowCasterMult_2.rgb;

                }
                
                //dmax.xyz = min(dmax * dIntensity, 1.0);
                dmax.w = 1.0;

                //Moonlight END

                float3 diffuse = col.rgb * _Color.rgb * dmax.rgb;
                float3 specular = specularAccum;
                return float4(diffuse + specular + emmis.rgb * _EmmissiveStrength * _EmmissiveColor.rgb, 1.0);
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}