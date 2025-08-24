#ifndef LIGHTING_COMMON_HLSL
#define LIGHTING_COMMON_HLSL

#define AreaLightType 0
#define EnvLightType 1
#define PointLightType 2

struct LightSource
{
    int lightIndex;
    int lightType; //0: area light, 1: env light, 2: point light
};

#endif
