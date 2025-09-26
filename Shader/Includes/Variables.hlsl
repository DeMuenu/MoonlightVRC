#ifndef MoonlightGlobalVariables
#define MoonlightGlobalVariables \
     \
    float _InverseSqareMultiplier; \
    float _LightCutoffDistance; \      
    \
    float4 _LightPositions[MAX_LIGHTS]; /* xyz = position  */ \
    float4 _LightColors[MAX_LIGHTS]; /* xyz = position  */ \
    float4 _LightDirections[MAX_LIGHTS]; /* xyz = direction, w = cos(halfAngle) */ \
    float _LightType[MAX_LIGHTS]; /* 0 = sphere, 1 = cone */ \
    float  _PlayerCount;                  /* set via SetFloat */ \
     \
     
#endif