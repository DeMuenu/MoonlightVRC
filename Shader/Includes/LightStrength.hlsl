#ifndef LightTypeCalculations
#define LightTypeCalculations(_Udon_LightColors ,LightCounter, i, NdotL, dIntensity, radius, Lightposition) \
    half typeId = _Udon_LightType[LightCounter]; \
    \
    if(typeId == 0) \
    { \
        float distAtten = max(1.0, distanceFromLight - radius) * invSqMul; \
        contrib = _Udon_LightColors[LightCounter].a / max(1e-4, distAtten * distAtten); \
    \
        dIntensity += contrib; \
    } \
    else if (typeId == 1) \
    { \
        float distAtten = distanceFromLight * invSqMul; \
        float invSq = _Udon_LightColors[LightCounter].a / max(1e-4, distAtten * distAtten); \
        \
        contrib = dot(-L, normalize(_Udon_LightDirections[LightCounter].xyz)); \
        contrib = smoothstep(radius,_Udon_LightDirections[LightCounter].w, contrib); \
        \
        contrib = contrib * invSq; \
        dIntensity += contrib; \
    } \
    half3 LightColor = _Udon_LightColors[LightCounter].xyz; \
 

#endif