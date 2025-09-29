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
    float  _Udon_ShadowMapIndex[MAX_LIGHTS];\
    float  _Udon_PlayerCount;                  /* set via SetFloat */ \


    float4x4 _Udon_WorldToLocal; \
    sampler2D _shadowCasterTex; \
    float4 _shadowCasterColor;  \
    float4 _OutSideColor;       \
    float _MinBrightnessShadow; \


#endif