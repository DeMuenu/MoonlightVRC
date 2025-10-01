#ifndef SHADOWCASTER_PLANE
#define SHADOWCASTER_PLANE

#include "UnityCG.cginc"   // for tex2Dlod, etc.

static const float WS_EPS = 1e-5;

inline float4 SampleShadowcasterPlaneWS_Basis(
    float3 A, float3 B,
    float3 P0, float3 Uinv, float3 Vinv, float3 N,
    sampler2D tex, float4 OutsideColor, float4 ShadowColor)
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

    float4 returnColor = tex2D(tex, float2(u + 0.5, v + 0.5)) * ShadowColor;
    returnColor = float4(returnColor.rgb * (1 - returnColor.a), 1);
    return returnColor;
}

#endif
