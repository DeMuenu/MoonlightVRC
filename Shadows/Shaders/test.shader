Shader "Custom/ProbeIndexVisualizer"
{
    Properties
    {
        _ProbeIndexTexArray ("Probe Index Texture Array", 2DArray) = "" {}
        _ProbePosTex ("Probe Position Texture", 2D) = "white" {}
        _LightmapIndex ("Lightmap Index (Slice)", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        Pass
        {
            Name "ProbeIndexDebug"
            Tags { "LightMode" = "ForwardBase" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma require 2darray

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv2          : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv2          : TEXCOORD0;
            };

            UNITY_DECLARE_TEX2DARRAY(_ProbeIndexTexArray);
            Texture2D<float4> _ProbePosTex;
            SamplerState sampler_ProbePosTex;

            float _LightmapIndex;

            // Generates a unique, high-contrast color for any given probe index
            float3 GetProbeColor(int id)
            {
                float n = (float)id * 99.123f;
                float r = frac(sin(n + 1.0f) * 43758.5453f);
                float g = frac(sin(n + 2.0f) * 22578.1453f);
                float b = frac(sin(n + 3.0f) * 19642.3498f);
                return float3(r, g, b);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS.xyz);
                output.uv2 = input.uv2 * unity_LightmapST.xy + unity_LightmapST.zw;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 sampleCoords = float3(input.uv2, _LightmapIndex);
                float4 encodedIndices = UNITY_SAMPLE_TEX2DARRAY(_ProbeIndexTexArray, sampleCoords);

                // Decode back to integer indices
                int3 probeIndices = round(encodedIndices.rgb * 65535.0f);

                // Visualize the closest probe (probeIndices.x) with a distinct random color
                float3 distinctColor = GetProbeColor(probeIndices.x);

                return float4(distinctColor, 1.0f);
            }
            ENDHLSL
        }
    }
}