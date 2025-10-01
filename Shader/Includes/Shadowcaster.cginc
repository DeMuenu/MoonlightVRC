#ifndef SHADOWCASTER_PLANE
#define SHADOWCASTER_PLANE

#include "UnityCG.cginc" // tex2D, tex2Dgrad

static const float WS_EPS = 1e-5;

inline float4 SampleShadowcasterPlaneWS_Basis(
    float3 A, float3 B,
    float3 P0, float3 Uinv, float3 Vinv, float3 N,
    sampler2D tex, float4 OutsideColor, float4 ShadowColor,
    float blurPixels, float2 texelSize)
{
    float3 d  = B - A;
    float  dn = dot(N, d);
    if (abs(dn) < WS_EPS) return OutsideColor;

    float  t  = dot(N, P0 - A) / dn;
    if (t < 0.0 || t > 1.0) return OutsideColor;

    float3 hit = A + d * t;
    float3 r   = hit - P0;

    // u,v in [-0.5, 0.5] if inside quad
    float u = dot(r, Uinv);
    float v = dot(r, Vinv);
    if (abs(u) > 0.5 || abs(v) > 0.5) return OutsideColor;

    float2 uv = float2(u + 0.5, v + 0.5);

    // If blur is tiny, do the normal one-tap
    if (blurPixels <= 0.001)
    {
        float4 col = tex2D(tex, uv) * ShadowColor;
        return float4(col.rgb * (1 - col.a), 1);
    }

    // Inflate gradients so the sampler picks a higher mip (cheap blur).
    float2 g = texelSize * blurPixels;
    float4 blurred = tex2Dgrad(tex, uv, float2(g.x, 0), float2(0, g.y));

    float4 outCol = blurred * ShadowColor;
    return float4(outCol.rgb * (1 - outCol.a), 1);
}

#endif
