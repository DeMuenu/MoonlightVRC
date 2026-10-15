Shader "DeMuenu/MoonlightVRC/backend/shadowCalculator"
{
    Properties
    {

        _OctreeTex ("16-bit Octree Texture", 2D) = "white" {}
        _BoundsCenter ("Bounds Center", Vector) = (0,0,0,0)
        _BoundsSize ("Bounds Size", Vector) = (10,10,10,0)
        _MaxWalkDepth ("Max Walk Depth", Int) = 5
        _ProbeCount ("ProbeCount", Int) = 200   

        _ProbePositionsTex ("ProbePositionsTex", 2D) = "white" {}

        _CameraRenderTextureSize ("Camera Render Texture Size (Width, Height)", Vector) = (1920, 1080, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        ZWrite Off 
        ZTest Always
        Cull Off // Prevents the quad from disappearing when viewed from certain angles

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD1;
            };

            Texture2D<float4> _OctreeTex;
            float4 _OctreeTex_TexelSize;
            float4 _BoundsCenter;
            float4 _BoundsSize;
            int _MaxWalkDepth;

            #include "Includes/OctreeWalker.cginc"


            sampler2D _ProbePositionsTex;
            float4 _ProbePositionsTex_TexelSize;
            sampler2D _OtherTex;
            float4 _CameraRenderTextureSize;
            float _CustomParam;

            #define MAX_LIGHTS 80 
            float4 _Udon_LightPositions[MAX_LIGHTS];
            float _Udon_PlayerCount;
            int _ProbeCount;
            

            v2f vert (appdata v)
            {
                v2f o;
                // Map standard quad local space (-0.5 to 0.5) directly to NDC screen space (-1 to 1)
                o.vertex = float4(v.vertex.xy * 2.0, 0.0, 1.0);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            float3 GetProbePosition(uint probeIndex)
            {
                uint textureWidth = (uint)_ProbePositionsTex_TexelSize.z;
                uint2 probePixel = uint2(probeIndex % textureWidth, probeIndex / textureWidth);
                float2 probeUV = (float2(probePixel) + 0.5) * _ProbePositionsTex_TexelSize.xy;
                return tex2D(_ProbePositionsTex, probeUV).xyz;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // Fix vertical flipping on platforms where projection or render texture coordinates are inverted
                #if UNITY_UV_STARTS_AT_TOP
                if (_ProjectionParams.x < 0)
                {
                    uv.y = 1.0 - uv.y;
                }
                #endif

                float2 cameraRenderTextureSize = max(_CameraRenderTextureSize.xy, float2(1.0, 1.0));
                int2 cameraPixelSize = int2(cameraRenderTextureSize);                
                int2 pixelCoord = clamp(int2(uv * cameraRenderTextureSize), int2(0, 0), cameraPixelSize - 1);
                
                uint2 unsignedPixelCoord = (uint2)pixelCoord;
                uint pixelWidth = (uint)cameraPixelSize.x;
                uint baseIndex = (unsignedPixelCoord.y * pixelWidth + unsignedPixelCoord.x) * 4u;
                uint probeCount = (uint)max(_ProbeCount, 1);

                uint sampleIndex1 = baseIndex;
                uint sampleIndex2 = baseIndex + 1u;
                uint sampleIndex3 = baseIndex + 2u;
                uint sampleIndex4 = baseIndex + 3u;

                // Calculate light indices
                uint lightIndex1 = sampleIndex1 / probeCount;
                uint lightIndex2 = sampleIndex2 / probeCount;
                uint lightIndex3 = sampleIndex3 / probeCount;
                uint lightIndex4 = sampleIndex4 / probeCount;

                // Return black if any light index exceeds the maximum allowed lights
                if (lightIndex1 >= MAX_LIGHTS || lightIndex2 >= MAX_LIGHTS || lightIndex3 >= MAX_LIGHTS || lightIndex4 >= MAX_LIGHTS)
                {
                    return fixed4(0.0, 0.0, 0.0, 1.0);
                }

                float3 hitPos;
                float channel1 = (float)CheckLineInterrupted(_Udon_LightPositions[lightIndex1].xyz, GetProbePosition(sampleIndex1 % probeCount), 32, hitPos);
                float channel2 = (float)CheckLineInterrupted(_Udon_LightPositions[lightIndex2].xyz, GetProbePosition(sampleIndex2 % probeCount), 32, hitPos);
                float channel3 = (float)CheckLineInterrupted(_Udon_LightPositions[lightIndex3].xyz, GetProbePosition(sampleIndex3 % probeCount), 32, hitPos);
                float channel4 = (float)CheckLineInterrupted(_Udon_LightPositions[lightIndex4].xyz, GetProbePosition(sampleIndex4 % probeCount), 32, hitPos);

                return fixed4(channel1, channel2, channel3, channel4);
            }
            ENDCG
        }
    }
}