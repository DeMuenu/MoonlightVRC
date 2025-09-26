#ifndef Lambert
#define Lambert(q ,i, N) \
    float3 L = normalize(q - i.worldPos);   /* q = light position */ \
    float  NdotL = saturate(dot(N, L) * 0.5 + 0.5);      /* one-sided Lambert */ \
    if (NdotL <= 0) continue; \
 
#endif