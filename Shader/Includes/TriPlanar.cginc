#ifndef TriplanarFunctions
#define SHADOWCASTER_PLANE

// Standard Triplanar Sampling
fixed4 SampleTriplanar(sampler2D tex, float3 worldPos, float3 blend, float4 st) {
    float2 uvX = worldPos.zy * st.xy + st.zw;
    float2 uvY = worldPos.xz * st.xy + st.zw;
    float2 uvZ = worldPos.xy * st.xy + st.zw;
    
    fixed4 colX = tex2D(tex, uvX);
    fixed4 colY = tex2D(tex, uvY);
    fixed4 colZ = tex2D(tex, uvZ);
    
    return colX * blend.x + colY * blend.y + colZ * blend.z;
}

// Triplanar Normal Mapping (Whiteout blend approximation)
float3 SampleTriplanarNormal(sampler2D tex, float3 worldPos, float3 worldNormal, float3 blend, float4 st, float strength) {
    float2 uvX = worldPos.zy * st.xy + st.zw;
    float2 uvY = worldPos.xz * st.xy + st.zw;
    float2 uvZ = worldPos.xy * st.xy + st.zw;
    
    float3 tX = UnpackNormal(tex2D(tex, uvX));
    float3 tY = UnpackNormal(tex2D(tex, uvY));
    float3 tZ = UnpackNormal(tex2D(tex, uvZ));
    
    // Apply strength
    tX.xy *= strength; tX = normalize(tX);
    tY.xy *= strength; tY = normalize(tY);
    tZ.xy *= strength; tZ = normalize(tZ);
    
    // Swizzle tangent normals to world space based on axis
    float3 nX = float3(tX.xy + worldNormal.zy, abs(tX.z) * worldNormal.x);
    float3 nY = float3(tY.xy + worldNormal.xz, abs(tY.z) * worldNormal.y);
    float3 nZ = float3(tZ.xy + worldNormal.xy, abs(tZ.z) * worldNormal.z);
    
    return normalize(nX.zyx * blend.x + nY.xzy * blend.y + nZ.xyz * blend.z);
}
// ----------------------------------


#endif