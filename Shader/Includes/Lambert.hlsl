#ifndef Lambert
#define Lambert(q ,i, N) \
    /* 'L' is inherited from InLoopSetup, avoiding an expensive normalize() */ \
    half  NdotL = saturate(dot(N, L) * 0.5 + 0.5);      /* one-sided Lambert */ \
    if (NdotL <= 0) continue; \
 
#endif