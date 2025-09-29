#ifndef SHADOWCASTER_PLANE
#define SHADOWCASTER_PLANE

#include "UnityCG.cginc"   // for tex2Dlod, etc.

static const float EPS = 1e-5;

// Returns whether segment hits the unit plane quad in plane-local z=0.
// Outputs uv in [0,1] and t in [0,1] along A->B.
inline bool RaySegmentHitsPlaneQuad(float4x4 worldToLocal, float3 rayOrigin, float3 rayEnd, out float2 uv, out float t)
{
    float3 aP = mul(worldToLocal, float4(rayOrigin, 1)).xyz;
    float3 bP = mul(worldToLocal, float4(rayEnd,   1)).xyz;

    float3 d  = bP - aP;
    float  dz = d.z;

    // Parallel-ish to plane?
    if (abs(dz) < EPS) return false;

    // Intersect z=0
    t = -aP.z / dz;

    // Segment only
    if (t < 0.0 || t > 1.0) return false;

    float3 hit = aP + d * t;

    // Inside 1x1 centered quad?
    if (abs(hit.x) > 0.5 || abs(hit.y) > 0.5) return false;

    uv = hit.xy + 0.5; // [-0.5,0.5] -> [0,1]
    return true;
}

// Fragment-shader version: uses proper filtering/mips via tex2D
inline float4 SampleShadowcasterPlane(float4x4 worldToLocal, sampler2D tex, float3 rayOrigin, float3 rayEnd, float4 OutsideColor)
{
    float2 uv; float t;
    if (RaySegmentHitsPlaneQuad(worldToLocal, rayOrigin, rayEnd, uv, t))
        return tex2D(tex, uv);   // full color

    return OutsideColor;
}

// Anywhere (vertex/geom/compute/custom code) version: forces LOD 0
inline float4 SampleShadowcasterPlaneLOD0(float4x4 worldToLocal, sampler2D tex, float3 rayOrigin, float3 rayEnd, float4 OutsideColor)
{
    float2 uv; float t;
    if (RaySegmentHitsPlaneQuad(worldToLocal, rayOrigin, rayEnd, uv, t))
        return tex2Dlod(tex, float4(uv, 0, 0));  // full color at mip 0

    return OutsideColor;
}

#endif
