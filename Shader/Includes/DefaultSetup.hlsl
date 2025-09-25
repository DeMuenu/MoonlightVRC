#ifndef InLoopSetup
#define InLoopSetup(_LightPositions, LightCounter, count, i) \
    if (LightCounter >= count) break; \
    \
    float distanceFromLight = length(i.worldPos - _LightPositions[LightCounter].xyz); \
    if (distanceFromLight > _LightCutoffDistance) continue; \
     \
    float contrib = 0.0; \
    
 
#endif