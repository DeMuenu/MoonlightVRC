#ifndef LightTypeCalculations
#define LightTypeCalculations(_LightColors ,LightCounter, i, NdotL, dIntensity, radius, Lightposition) \
    float invSqMul = max(1e-4, _InverseSqareMultiplier); \
    \
    if(_LightType[LightCounter] == 0) \
    { \
        contrib = _LightColors[LightCounter].a / max(1e-4, max(0, max(1, distanceFromLight - radius) * invSqMul) * max(0, max(1, distanceFromLight - radius) * invSqMul)); \
    \
        dIntensity += contrib * NdotL; \
    } \
    else if (_LightType[LightCounter] == 1) \
    { \
        float invSq    = _LightColors[LightCounter].a / max(1e-4, (distanceFromLight * invSqMul) * (distanceFromLight * invSqMul)); \
        float threshold = (-1 + _LightDirections[LightCounter].w / 180); \
        \
        contrib = min(dot(normalize(i.worldPos - Lightposition), -normalize(_LightDirections[LightCounter].xyz)), 0); \
        contrib= 1 - step(threshold, contrib); \
        \
        contrib = contrib * invSq; \
        dIntensity += contrib * NdotL; \
    } \
    float3 LightColor = _LightColors[LightCounter].xyz; \
 

#endif