#ifndef MOONLIGHT_GLOBAL_VARIABLES_INCLUDED
#define MOONLIGHT_GLOBAL_VARIABLES_INCLUDED

float _InverseSqareMultiplier;
float _LightCutoffDistance;

float4 _Udon_LightPositions[MAX_LIGHTS]; /* xyz = position  */
float4 _Udon_LightColors[MAX_LIGHTS]; /* xyz = position  */
float4 _Udon_LightDirections[MAX_LIGHTS]; /* xyz = direction, w = cos(halfAngle) */
float  _Udon_LightType[MAX_LIGHTS]; /* 0 = sphere, 1 = cone */
float  _Udon_ShadowMapIndex[MAX_LIGHTS];
float  _Udon_PlayerCount;                  /* set via SetFloat */

float4 _Udon_Plane_Origin_1;   // xyz = origin (world), w unused
float4 _Udon_Plane_Uinv_1;     // xyz = Udir / (2*halfWidth)
float4 _Udon_Plane_Vinv_1;     // xyz = Vdir / (2*halfHeight)
float4 _Udon_Plane_Normal_1;   // xyz = unit normal

sampler2D _Udon_shadowCasterTex_1;
float4 _Udon_shadowCasterColor_1;
float4 _Udon_OutSideColor_1;
float _Udon_MinBrightnessShadow_1;

float4 _Udon_Plane_Origin_2;
float4 _Udon_Plane_Uinv_2;
float4 _Udon_Plane_Vinv_2;
float4 _Udon_Plane_Normal_2;

sampler2D _Udon_shadowCasterTex_2;
float4 _Udon_shadowCasterColor_2;
float4 _Udon_OutSideColor_2;
float _Udon_MinBrightnessShadow_2;

float _BlurPixels;
float4 _Udon_shadowCasterTex_1_TexelSize; // xy = 1/width, 1/height
float4 _Udon_shadowCasterTex_2_TexelSize;

bool _EnableShadowCasting;

#endif