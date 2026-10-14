Shader "DeMuenu/MoonlightVRC/backend/shadowCalculator"
{
    Properties
    {

        _OctreeTex ("16-bit Octree Texture", 2D) = "white" {}
        _BoundsCenter ("Bounds Center", Vector) = (0,0,0,0)
        _BoundsSize ("Bounds Size", Vector) = (10,10,10,0)
        _MaxWalkDepth ("Max Walk Depth", Int) = 5
        _ProbeCount ("ProbeCount", Int) = 5   

        _ProbePositionsTex ("Texture", 2D) = "white" {}

        _CameraRenderTextureSize ("Camera Render Texture Size (Width, Height)", Vector) = (1920, 1080, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        ZWrite Off 
        ZTest Always
        Cull Back // Prevents the quad from disappearing when viewed from certain angles

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Includes/OctreeWalker.cginc"

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


            sampler2D _ProbePositionsTex;
            sampler2D _OtherTex;
            float4 _CameraRenderTextureSize;
            float _CustomParam;

            #define MAX_LIGHTS 80 
            float4 _Udon_LightPositions[MAX_LIGHTS];
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

            fixed4 frag (v2f i) : SV_Target
            {
                float2 cameraRenderTextureSize = max(_CameraRenderTextureSize.xy, float2(1.0, 1.0));
                int2 cameraPixelSize = int2(cameraRenderTextureSize);
                
                int2 pixelCoord = clamp(int2(i.uv * cameraRenderTextureSize), int2(0, 0), cameraPixelSize - 1);
                float2 targetUV = (float2(pixelCoord) + 0.5) / cameraRenderTextureSize;

                int perLightPixels

                fixed4 baseCol = tex2D(_ProbePositionsTex, i.uv);
                fixed4 finalCol = checker ? baseCol : otherTexPixel;

                return finalCol;
            }
            ENDCG
        }
    }
}