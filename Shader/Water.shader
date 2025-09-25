Shader "DeMuenu/World/Hoppou/Water"
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

        //MoonsLight
        _InverseSqareMultiplier ("Inverse Square Multiplier", Float) = 1
        _LightCutoffDistance ("Light Cutoff Distance", Float) = 100

        _SpecPower ("Spec Power", Range(4,256)) = 64
        _SpecIntensity ("Spec Intensity", Range(0,10)) = 1
        _AmbientFloor ("Ambient Floor", Range(0,1)) = 0.08

        _F0 ("F0", Range(0,1)) = 0.02
        _FresnelPower ("Fresnel Power", Range(1,8)) = 5
        _ReflectionStrength ("Reflection Strength", Range(0,1)) = 0.7
        //MoonsLight END

        _WaveInput ("Wave Input", 2D) = "black" {}
        _WaveTex ("Wave Texture", 2D) = "black" {}
        _CameraScale ("Camera Scale", Float) = 15
        _CameraPositionZ ("Camera Position Z", Float) = 0
        _CameraPositionX ("Camera Position X", Float) = 0

        _WaveScale ("Wave Scale", Range(0.001, 100)) = 1
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

            //MoonsLight Defines
            #define MAX_LIGHTS 80 // >= maxPlayers in script
            //MoonsLight Defines END

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float2 uvnorm : TEXCOORD1;
                float4 vertex : SV_POSITION;

                //MoonsLight
                float3 worldPos : TEXCOORD2;
                float3 worldNormal: TEXCOORD3;
                //MoonsLight END
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


            //MoonsLight variables
            float _InverseSqareMultiplier;
            float _LightCutoffDistance;            

            float4 _LightPositions[MAX_LIGHTS]; // xyz = position
            float4 _LightColors[MAX_LIGHTS]; // xyz = position
            float4 _LightDirections[MAX_LIGHTS]; // xyz = direction, w = cos(halfAngle)
            float _LightType[MAX_LIGHTS]; // 0 = sphere, 1 = cone
            float  _PlayerCount;                  // set via SetFloat

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
            //Watershader specific END


            float _F0, _FresnelPower, _ReflectionStrength;

            inline float SchlickFresnel(float NoV, float F0, float power)
            {
                float f = pow(saturate(1.0 - NoV), power);
                return saturate(F0 + (1.0 - F0) * f);
            }
            //MoonsLight variables END

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.uvnorm = TRANSFORM_TEX(v.uv, _NormalMap);
                //MoonsLight Vertex
                float4 wp = mul(unity_ObjectToWorld, v.vertex);
                o.worldPos = wp.xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                //MoonsLight Vertex END
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

                float2 waveUV = float2(_CameraPositionX - i.worldPos.x, _CameraPositionZ - i.worldPos.z) / _CameraScale / 2 + 0.5;
                fixed4 Wave = tex2D(_WaveInput, waveUV);
                if ((waveUV.x < 0.1) || (waveUV.x > 0.9) || (waveUV.y < 0.1) || (waveUV.y > 0.9)){
                    Wave = float4(0,0,0,0);
                }
                
                //i.vertex += float4(0, Wave.g * _WaveScale, 0, 0);
                //i.worldPos += float3(0, Wave.g * _WaveScale, 0);


                //MoonsLight
                int count = (int)_PlayerCount;

                float3 N = normalize(i.worldNormal + NormalOffset1 * _NormalMapStrength1 + NormalOffset2 * _NormalMapStrength2 + Wave * _WaveScale); //for lambertian diffuse


                //Waterspecific
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 R = reflect(-V, N);  //for reflection vector
                //Waterspecific END
                //return float4(R,1);

                

                
                // Example: compute distance to nearest player
                float4 dmax = float4(0,0,0,1);
                float dIntensity = 0;
                [loop]
                for (int LightCounter = 0; LightCounter < MAX_LIGHTS; LightCounter++)
                {
                    InLoopSetup(_LightPositions, LightCounter, count, i); //defines distanceFromLight, contrib
                    
                    Lambert(_LightPositions[LightCounter].xyz ,i, N);

                    LightTypeCalculations(_LightColors, LightCounter, i, NdotL, dIntensity, _LightPositions[LightCounter].a, _LightPositions[LightCounter].xyz);



                    //Watershader specific
                    //float fres = Schlick(saturate(dot(N, V)), _F0, _FresnelPower);
                    float3 R = reflect(-V, N);
                    float  spec = pow(saturate(dot(R, L)), _SpecPower);
                    //return float4(spec, spec, spec,1);
                    dmax.rgb += _LightColors[LightCounter].rgb * contrib + _LightColors[LightCounter].rgb * _SpecIntensity * spec * contrib;
                    dmax.a -=  _SpecIntensity * spec;
                    //dmax = dmax + contrib * float4(LightColor, 1); // accumulate light contributions



                }
                
                //dmax.xyz = min(dmax * dIntensity, 1.0);

                float  NoV   = saturate(dot(N, V));
                float  fres  = SchlickFresnel(NoV, _F0, _FresnelPower);

                dmax.w = 1.0;
                dmax.a = dmax.a * _ReflectionStrength * fres;


                //MoonsLight END



                // Final color
                return col * _Color * dmax ;
            }
            ENDCG
        }
    }
}
