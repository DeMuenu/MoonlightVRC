#ifndef InLoopSetup
#define InLoopSetup(_Udon_LightPositions, LightCounter, count, i) \
    if (LightCounter >= count) break; \
    \
    float distanceFromLight = length(i.worldPos - _Udon_LightPositions[LightCounter].xyz); \
    if (distanceFromLight > _LightCutoffDistance) continue; \
    \
    float contrib = 0.0; 

#endif

#ifndef OutLoopSetup
#define OutLoopSetup(i, _Udon_PlayerCount) \
    int count = (int)_Udon_PlayerCount; \
    \
    float4 dmax = float4(0,0,0,1); \
    float dIntensity = 0; 


#endif