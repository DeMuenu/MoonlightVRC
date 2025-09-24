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

                //MoonsLight
                int count = (int)_PlayerCount;

                float3 N = normalize(i.worldNormal + NormalOffset1 * _NormalMapStrength1 + NormalOffset2 * _NormalMapStrength2); //for lambertian diffuse


                //Waterspecific
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 R = reflect(-V, N);  //for reflection vector
                //Waterspecific END
                //return float4(R,1);

                

                
                // Example: compute distance to nearest player
                float4 dmax = float4(0,0,0,1);
                float dIntensity = 0;
                [loop]
                for (int idx = 0; idx < MAX_LIGHTS; idx++)
                {
                    if (idx >= count) break;
                    float radius = _LightPositions[idx].a;
                    float3 q = _LightPositions[idx].xyz;

                    float distanceFromLight = length(i.worldPos - q);
                    if (distanceFromLight > _LightCutoffDistance) continue;
                    
                    float sd = 0.0;
                    float contrib = 0.0;
                    

                    float invSqMul = max(1e-4, _InverseSqareMultiplier);
                    

                    //Lambertian diffuse
                    float3 L = normalize(q - i.worldPos);   // q = light position
                    float  NdotL = saturate(dot(N, L) * 0.5 + 0.5);      // one-sided Lambert
                    if (NdotL <= 0) continue;

                    if(_LightType[idx] == 0)
                    {
                        float invSq    = _LightColors[idx].a / max(1e-4, max(0, max(1, distanceFromLight - radius) * invSqMul) * max(0, max(1, distanceFromLight - radius) * invSqMul));
                        contrib  = invSq;
                        //contrib = contrib * step(-distance(i.worldPos, q), -1 + radius * 1); // 0 if outside sphere
                        dIntensity += contrib * NdotL;
                    }
                    else if (_LightType[idx] == 1)
                    {
                        float invSq    = _LightColors[idx].a / max(1e-4, (distanceFromLight * invSqMul) * (distanceFromLight * invSqMul));
                        float threshold = (-1 + _LightDirections[idx].w / 180);
                        
                        contrib = min(dot(normalize(i.worldPos - q), -normalize(_LightDirections[idx].xyz)), 0);
                        contrib= 1 - step(threshold, contrib);
                        
                        contrib = contrib * invSq;
                        dIntensity += contrib * NdotL;
                    }
                    float3 LightColor = _LightColors[idx].xyz; // * NormalDirMult;

                    //Watershader specific
                    //float fres = Schlick(saturate(dot(N, V)), _F0, _FresnelPower);
                    float3 R = reflect(-V, N);
                    float  spec = pow(saturate(dot(R, L)), _SpecPower);
                    //return float4(spec, spec, spec,1);
                    dmax.rgb += _LightColors[idx].rgb * contrib + _LightColors[idx].rgb * _SpecIntensity * spec * contrib;
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
