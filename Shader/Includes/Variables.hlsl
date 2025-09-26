#ifndef MoonlightGlobalVariables
#define MoonlightGlobalVariables \
     \
    float _InverseSqareMultiplier; \
    float _LightCutoffDistance; \
    \
    float4 _Udon_LightPositions[MAX_LIGHTS]; /* xyz = position  */ \
    float4 _Udon_LightColors[MAX_LIGHTS]; /* xyz = position  */ \
    float4 _Udon_LightDirections[MAX_LIGHTS]; /* xyz = direction, w = cos(halfAngle) */ \
    float  _Udon_LightType[MAX_LIGHTS]; /* 0 = sphere, 1 = cone */ \
    float  _Udon_PlayerCount;                  /* set via SetFloat */ \

#endif