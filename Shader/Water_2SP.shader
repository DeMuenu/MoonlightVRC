Shader "DeMuenu/World/Hoppou/WaterFlat_2SP"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,0.5)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalMapStrength1 ("Normal Map Strength", Range(0,1)) = 1
        _NormalMapStrength2 ("Normal Map Strength 2", Range(0,1)) = 0.5
        _NormalMap2Tiling ("Normal Map 2 Tiling", Float) = 2
        _NormalMapScrollSpeed ("Normal Map Scroll Speed", Float) = 0.1
        _NormalMapScrollSpeed2 ("Normal Map 2 Scroll Speed", Float) = 0.05

        _MinTransparency ("Min Transparency", Range(0,1)) = 0

        //Moonlight
        _InverseSqareMultiplier ("Inverse Square Multiplier", Float) = 1
        _LightCutoffDistance ("Light Cutoff Distance", Float) = 100

        _EnableShadowCasting ("Enable Shadowcasting", Float) = 0
        _BlurPixels ("Shadowcaster Blur Pixels", Float) = 0
        //Moonlight END

        _SpecPower ("Spec Power", Range(4,256)) = 64
        _SpecIntensity ("Spec Intensity", Range(0,10)) = 1
        _AmbientFloor ("Ambient Floor", Range(0,1)) = 0.08

        _F0 ("F0", Range(0,1)) = 0.02
        _FresnelPower ("Fresnel Power", Range(1,8)) = 5
        _ReflectionStrength ("Reflection Strength", Range(0,1)) = 0.7

        _WaveInput ("Wave Input", 2D) = "black" {}
        _CameraScale ("Camera Scale", Float) = 15
        _CameraPositionZ ("Camera Position Z", Float) = 0
        _CameraPositionX ("Camera Position X", Float) = 0
        _WaveScale ("Wave Scale", Range(0.001, 2)) = 1
        _WaveColor ("Wave Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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
            #include "Includes/Shadowcaster.cginc"

            //Moonlight Defines
            #define MAX_LIGHTS 80 // >= maxPlayers in script
            //Moonlight Defines END

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent: TANGENT; 
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uvnorm : TEXCOORD1;
                float4 vertex : SV_POSITION;

                //Moonlight
                float3 worldPos : TEXCOORD2;
                float3 worldNormal: TEXCOORD3;
                //Moonlight END

                float3 worldTangent   : TEXCOORD4;
                float3 worldBitangent : TEXCOORD5;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            sampler2D _NormalMap;
            float4 _NormalMap_ST;
            float _NormalMapStrength1;
            float _NormalMapStrength2;
            float _NormalMap2Tiling;
            float _NormalMapScrollSpeed;
            float _NormalMapScrollSpeed2;
            float _MinTransparency;

            
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

            float _BlurPixels;
            float4 _Udon_shadowCasterTex_1_TexelSize; // xy = 1/width, 1/height
            float4 _Udon_shadowCasterTex_2_TexelSize;

            bool _EnableShadowCasting;


            //Watershader specific
            float _SpecPower, _SpecIntensity;   
            float3 _AmbientFloor;

            sampler2D _WaveInput; 
            sampler2D _WaveTex;
            float2 _WaveTex_ST;
            float _CameraScale;
            float _CameraPositionZ;
            float _CameraPositionX;
            float _WaveScale;
            float4 _WaveColor;
            //Watershader specific END


            float _F0, _FresnelPower, _ReflectionStrength;

            inline float SchlickFresnel(float NoV, float F0, float power)
            {
                float f = pow(saturate(1.0 - NoV), power);
                return saturate(F0 + (1.0 - F0) * f);
            }
            //Moonlight variables END

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvnorm = TRANSFORM_TEX(v.uv, _NormalMap);
                //Moonlight Vertex
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = wp.xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                //Moonlight Vertex END

                float3 tWS = normalize(UnityObjectToWorldDir(v.tangent.xyz));
                float3 bWS = normalize(cross(o.worldNormal, tWS) * v.tangent.w);

                o.worldTangent  = tWS;
                o.worldBitangent= bWS;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 norm = tex2D(_NormalMap, i.uvnorm + float2(0, _NormalMapScrollSpeed * sin(_Time.y)));
                fixed4 norm2 = tex2D(_NormalMap, i.uvnorm * _NormalMap2Tiling + float2(_NormalMapScrollSpeed2 * sin(_Time.y), 0));
                float3 NormalOffset1 = UnpackNormal(norm).xyz;
                float3 NormalOffset2 = UnpackNormal(norm2).xyz;



                //Moonlight

                half3 n1 = UnpackNormal(norm);
                half3 n2 = UnpackNormal(norm2);

                n1.xy *= _NormalMapStrength1;
                n2.xy *= _NormalMapStrength2;
                n1 = normalize(n1);
                n2 = normalize(n2);

                // combine two tangent-space normals (whiteout mix; good enough for water)
                half3 nTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));

                // rotate TS -> WS with the TBN (linear combo is cheaper than a matrix mul)
                half3 N = normalize(
                    i.worldTangent   * nTS.x +
                    i.worldBitangent * nTS.y +
                    i.worldNormal    * nTS.z);
                

                //Waterspecific
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 R = reflect(-V, N);  //for reflection vector
                //Waterspecific END

                OutLoopSetup(i, _Udon_PlayerCount) //defines count, N, dmax, dIntensity

                [loop]
                for (int LightCounter = 0; LightCounter < MAX_LIGHTS; LightCounter++)
                {
                    InLoopSetup(_Udon_LightPositions, LightCounter, count, i); //defines distanceFromLight, contrib
                    
                    Lambert(_Udon_LightPositions[LightCounter].xyz ,i, N);

                    LightTypeCalculations(_Udon_LightColors, LightCounter, i, 1, dIntensity, _Udon_LightPositions[LightCounter].a, _Udon_LightPositions[LightCounter].xyz);

                    float4 ShadowCasterMult_1 = 1;
                    float4 ShadowCasterMult_2 = 1;

                    if (((_Udon_ShadowMapIndex[LightCounter] > 0.5) && (_Udon_ShadowMapIndex[LightCounter] < 1.5) && (_EnableShadowCasting > 0.5)) || (_Udon_ShadowMapIndex[LightCounter] > 2.5 && _EnableShadowCasting))
                    {
                        float4 sc1 = SampleShadowcasterPlaneWS_Basis(
                            _Udon_LightPositions[LightCounter].xyz, i.worldPos,
                            _Udon_Plane_Origin_1.xyz, _Udon_Plane_Uinv_1.xyz, _Udon_Plane_Vinv_1.xyz, _Udon_Plane_Normal_1.xyz,
                            _Udon_shadowCasterTex_1, _Udon_OutSideColor_1, _Udon_shadowCasterColor_1, _BlurPixels, _Udon_shadowCasterTex_1_TexelSize.xy);
                        ShadowCasterMult_1 = max(sc1, _Udon_MinBrightnessShadow_1);
                    }
                    if (_Udon_ShadowMapIndex[LightCounter] > 1.5 && (_EnableShadowCasting > 0.5))                    {
                        float4 sc2 = SampleShadowcasterPlaneWS_Basis(
                            _Udon_LightPositions[LightCounter].xyz, i.worldPos,
                            _Udon_Plane_Origin_2.xyz, _Udon_Plane_Uinv_2.xyz, _Udon_Plane_Vinv_2.xyz, _Udon_Plane_Normal_2.xyz,
                            _Udon_shadowCasterTex_2, _Udon_OutSideColor_2, _Udon_shadowCasterColor_2, _BlurPixels, _Udon_shadowCasterTex_2_TexelSize.xy);
                        ShadowCasterMult_2 = max(sc2, _Udon_MinBrightnessShadow_2);
                    }

                    //Watershader specific
                    //float fres = Schlick(saturate(dot(N, V)), _F0, _FresnelPower);
                    float  spec = pow(saturate(dot(R, L)), _SpecPower);
                    //return float4(spec, spec, spec,1);
                    dmax.rgb += _Udon_LightColors[LightCounter].rgb * contrib * ShadowCasterMult_1 * ShadowCasterMult_2 + _Udon_LightColors[LightCounter].rgb * _SpecIntensity * spec * contrib * ShadowCasterMult_1 * ShadowCasterMult_2;
                    dmax.a -=  _SpecIntensity * spec;
                    //dmax = dmax + contrib * float4(LightColor, 1); // accumulate light contributions



                }
                
                //dmax.xyz = min(dmax * dIntensity, 1.0);

                float  NoV   = saturate(dot(N, V));
                float  fres  = SchlickFresnel(NoV, _F0, _FresnelPower);

                dmax.w = 1.0;
                dmax.a = dmax.a * _ReflectionStrength * fres; 

                //Moonlight END
                float4 finalColor = col * _Color * dmax;

                float2 waveUV = float2(_CameraPositionX - i.worldPos.x, _CameraPositionZ - i.worldPos.z) / _CameraScale / 2 + 0.5;
                fixed4 Wave = tex2D(_WaveInput, waveUV);
                if ((waveUV.x < 0.1) || (waveUV.x > 0.9) || (waveUV.y < 0.1) || (waveUV.y > 0.9)){
                    Wave = float4(0,0,0,0);
                }
                Wave.a = Wave.r;
                Wave *= dmax;

                float2 camXZ = float2(_CameraPositionX, _CameraPositionZ);
                float2 posXZ = float2(i.worldPos.x, i.worldPos.z);

                float dist = distance(posXZ, camXZ);

                float distFade = 1.0 - smoothstep(0, _CameraScale, dist);

                float4 waveCol = Wave * _WaveScale * _WaveColor;

                float k = saturate(1 * distFade);
                
                float4 outCol = finalColor;
                outCol.rgb = lerp(outCol.rgb, waveCol.rgb, k);

                outCol.a = max(outCol.a, _MinTransparency);
                return outCol;
                // Final color
            }
            ENDCG
        }
    }
}
