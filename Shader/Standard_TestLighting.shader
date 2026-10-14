Shader "DeMuenu/MoonlightVRC/Standard_ShadowTest"
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

        //Moonlight
        _InverseSqareMultiplier ("Inverse Square Multiplier", Float) = 1
        _LightCutoffDistance ("Light Cutoff Distance", Float) = 100

        _EnableShadowCasting ("Enable Shadowcasting", Float) = 0
        _BlurPixels ("Shadowcaster Blur Pixels", Float) = 0
        //Moonlight END

        _ShadowCalculationTex ("ShadowTexture", 2D) = "white" {}
        _ProbeCount ("ProbeCount", Int) = 200

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2

        // Probe Blending Properties
        _ProbeIndexTexArray ("Probe Index Texture Array", 2DArray) = "" {}
        _ProbePosTex ("Probe Position Texture", 2D) = "white" {}
        _LightmapIndex ("Lightmap Index (Slice)", Float) = 0.0
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
            #pragma target 3.5
            #pragma require 2darray

            //Moonlight Defines
            #define MAX_LIGHTS 80
            //Moonlight Defines END
            
            #include "UnityCG.cginc"
            #include "Includes/Moonlight.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1; // Lightmap UVs
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1; // Lightmap UVs transformed
                float2 uvEmmis : TEXCOORD4;
                float4 vertex : SV_POSITION;
                float2 normUV : TEXCOORD5;
                float3 worldTangent   : TEXCOORD6;
                float3 worldBitangent : TEXCOORD7;

                //Moonlight
                float3 worldPos : TEXCOORD2;
                float3 worldNormal: TEXCOORD3;
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

            sampler2D _ShadowCalculationTex;
            float4 _ShadowCalculationTex_TexelSize;

            int _ProbeCount;

            // Probe Textures & Properties
            UNITY_DECLARE_TEX2DARRAY(_ProbeIndexTexArray);
            sampler2D _ProbePosTex;
            float4 _ProbePosTex_TexelSize;
            float _LightmapIndex;

            // Helper to fetch probe world position from the 256x256 position texture
            float3 GetProbePosition(int index)
            {
                float x = (float)(index % 256) + 0.5f;
                float y = (float)(index / 256) + 0.5f;
                float2 uv = float2(x / 256.0f, y / 256.0f);
                return tex2Dlod(_ProbePosTex, float4(uv, 0, 0)).xyz;
            }

            // USER FUNCTION: Hook your probe ID and light ID data source here
            float GetProbeLightValue(int probeId, int lightId)
            {
                // 1. Calculate the linear sample index (matches the packer order: light changes every _ProbeCount steps)
                uint sampleIndex = (uint)lightId * (uint)_ProbeCount + (uint)probeId;

                // 2. Each pixel packs 4 samples into RGBA
                uint pixelLinearIndex = sampleIndex / 4u;
                uint channelIndex = sampleIndex % 4u; // 0 = R, 1 = G, 2 = B, 3 = A

                // 3. Map the linear pixel index into 2D texture coordinates
                uint width = (uint)_ShadowCalculationTex_TexelSize.z;
                uint px = pixelLinearIndex % width;
                uint py = pixelLinearIndex / width;

                // 4. Sample the exact pixel center from the render texture
                float2 uv = (float2(px, py) + 0.5f) * _ShadowCalculationTex_TexelSize.xy;
                float4 col = tex2D(_ShadowCalculationTex, uv);

                // 5. Extract the specific channel corresponding to this probe-light pair
                float val = (channelIndex == 0) ? col.r : 
                            (channelIndex == 1) ? col.g : 
                            (channelIndex == 2) ? col.b : col.a;

                return 1 - val;
            }

 // Blending function for the 3 closest probes (Squared distance only)
float BlendClosestProbes(float3 worldPos, float2 lightmapUV, float lightmapIndex, int lightId)
{
    // Sample the 3 closest probe indices from the Texture2DArray
    float3 sampleCoords = float3(lightmapUV, lightmapIndex);
    float4 encodedIndices = UNITY_SAMPLE_TEX2DARRAY(_ProbeIndexTexArray, sampleCoords);
    int3 probeIndices = round(encodedIndices.rgb * 65535.0f);

    // Fetch world positions directly
    float3 p0 = GetProbePosition(probeIndices.x);
    float3 p1 = GetProbePosition(probeIndices.y);
    float3 p2 = GetProbePosition(probeIndices.z);

    // Vector offsets from pixel to probes
    float3 v0 = p0 - worldPos;
    float3 v1 = p1 - worldPos;
    float3 v2 = p2 - worldPos;

    // Squared distances (avoids costly sqrt operations)
    float sqrDist0 = dot(v0, v0);
    float sqrDist1 = dot(v1, v1);
    float sqrDist2 = dot(v2, v2);

    // Inverse distance weights
    float w0 = 1.0f / (sqrDist0 + 1e-5f);
    float w1 = 1.0f / (sqrDist1 + 1e-5f);
    float w2 = 1.0f / (sqrDist2 + 1e-5f);

    float weightSum = w0 + w1 + w2 + 1e-5f;
    w0 /= weightSum;
    w1 /= weightSum;
    w2 /= weightSum;

    // Fetch lighting values and blend
    return (GetProbeLightValue(probeIndices.x, lightId) * w0) + 
           (GetProbeLightValue(probeIndices.y, lightId) * w1) + 
           (GetProbeLightValue(probeIndices.z, lightId) * w2);
}

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normUV = TRANSFORM_TEX(v.uv, _NormalMap);
                o.uvEmmis = TRANSFORM_TEX(v.uv, _EmmisiveText);

                // Transform lightmap UVs
                o.uv2 = v.uv1 * unity_LightmapST.xy + unity_LightmapST.zw;

                float3 nWS = UnityObjectToWorldNormal(v.normal);
                float3 tWS = normalize(UnityObjectToWorldDir(v.tangent.xyz));
                float3 bWS = normalize(cross(nWS, tWS) * v.tangent.w);

                o.worldNormal   = nWS;
                o.worldTangent  = tWS;
                o.worldBitangent= bWS;

                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = wp.xyz;
                
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 norm = tex2D(_NormalMap, i.normUV);
                fixed4 emmis = tex2D(_EmmisiveText, i.uvEmmis);

                float3 nTS = UnpackNormal(norm);
                float3 NmapWS = normalize(i.worldTangent * nTS.x +
                                        i.worldBitangent * nTS.y +
                                        i.worldNormal   * nTS.z);
                float3 N = normalize(lerp(normalize(i.worldNormal), NmapWS, saturate(_NormalMapStrength)));

                OutLoopSetup(i, _Udon_PlayerCount)

                [loop]
                for (int LightCounter = 0; LightCounter < count; LightCounter++)
                {
                    InLoopSetup(_Udon_LightPositions, LightCounter, count, i);

                    float3 hitPos;


                    Lambert(_Udon_LightPositions[LightCounter].xyz, i, N);

                    LightTypeCalculations(_Udon_LightColors, LightCounter, i, NdotL, dIntensity, _Udon_LightPositions[LightCounter].a, _Udon_LightPositions[LightCounter].xyz);
                    
                    // EXAMPLE: Blend probe-based values using the current Light ID (LightCounter) and pixel world pos/uv2
                    float blendedProbeValue = BlendClosestProbes(i.worldPos, i.uv2, _LightmapIndex, LightCounter);

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

                    // You can factor blendedProbeValue into your contribution calculation here:
                    dmax = dmax + contrib * float4(LightColor, 1) * NdotL * ShadowCasterMult_1 * ShadowCasterMult_2 * blendedProbeValue; 
                }
                
                dmax.w = 1.0;

                return col * _Color * dmax + emmis * _EmmissiveStrength * _EmmissiveColor;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}