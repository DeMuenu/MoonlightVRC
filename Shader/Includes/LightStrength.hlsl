#ifndef LightTypeCalculations
#define LightTypeCalculations(_Udon_LightColors ,LightCounter, i, NdotL, dIntensity, radius, Lightposition) \
    float invSqMul = max(1e-4, _InverseSqareMultiplier); \
    \
    if(_Udon_LightType[LightCounter] == 0) \
    { \
        contrib = _Udon_LightColors[LightCounter].a / max(1e-4, max(0, max(1, distanceFromLight - radius) * invSqMul) * max(0, max(1, distanceFromLight - radius) * invSqMul)); \
    \
        dIntensity += contrib; \
    } \
    else if (_Udon_LightType[LightCounter] == 1) \
    { \
        float invSq    = _Udon_LightColors[LightCounter].a / max(1e-4, (distanceFromLight * invSqMul) * (distanceFromLight * invSqMul)); \
        \
        contrib = dot(normalize(i.worldPos - Lightposition), normalize(_Udon_LightDirections[LightCounter].xyz)); \
        contrib = smoothstep(radius,_Udon_LightDirections[LightCounter].w, contrib); \
        \
        contrib = contrib * invSq; \
        dIntensity += contrib; \
    } \
    float3 LightColor = _Udon_LightColors[LightCounter].xyz; \
 

#endif