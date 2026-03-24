#ifndef InLoopSetup
#define InLoopSetup(_Udon_LightPositions, LightCounter, count, i) \
    if (LightCounter >= count) break; \
    \
    float3 lightVec = _Udon_LightPositions[LightCounter].xyz - i.worldPos; \
    float distanceFromLight = length(lightVec); \
    if (distanceFromLight > _LightCutoffDistance) continue; \
    \
    float contrib = 0.0; \
    float3 L = lightVec / max(distanceFromLight, 1e-4);

#endif

#ifndef OutLoopSetup
#define OutLoopSetup(i, _Udon_PlayerCount) \
    int count = (int)_Udon_PlayerCount; \
    float invSqMul = max(1e-4, _InverseSqareMultiplier); \
    bool shadowCastingEnabled = _EnableShadowCasting > 0.5; \
    \
    float4 dmax = float4(0,0,0,1); \
    float dIntensity = 0; 


#endif