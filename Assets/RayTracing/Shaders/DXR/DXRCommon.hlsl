#ifndef DXRCOMMON_HLSL
#define DXRCOMMON_HLSL

#include "definitions.hlsl"
#include "mathdef.hlsl"

#define GLOBAL_RESOURCE(type, name, reg) type name : register(reg, space1);

#define _ENVIRONMENT_MAP_ENABLE _EnvironmentMapEnable == 1

typedef BuiltInTriangleIntersectionAttributes AttributeData;

RaytracingAccelerationStructure _AccelerationStructure;

RWStructuredBuffer<RNG>    _RNGs;
Texture2D<half4>  _RayConeGBuffer;

float2 ConcentricSampleDisk(float2 u)
{
    float2 u1 = float2(u.x * 2.0f - 1, u.y * 2.0f - 1);

    if (u1.x == 0 && u1.y == 0)
        return float2(0, 0);

    float theta, r;
    if (abs(u1.x) > abs(u1.y))
    {
        r = u1.x;
        theta = u1.y / u1.x * PI_OVER_4;
    }
    else
    {
        r = u1.y;
        theta = PI_OVER_2 - u1.x / u1.y * PI_OVER_4;
    }
    return r * float2(cos(theta), sin(theta));
}

float3 CosineSampleHemisphere(float2 u)
{
    float2 rphi = ConcentricSampleDisk(u);
    float z = sqrt(1.0f - rphi.x * rphi.x - rphi.y * rphi.y);
    return float3(rphi.x, rphi.y, z);
}

float UniformFloat(inout RNG rng)
{
    uint lcg_a = 1664525u;
    uint lcg_c = 1013904223u;
    rng.state = lcg_a * rng.state + lcg_c;
    //rng.s1 = 0;
    return (rng.state & 0x00ffffffu) * (1.0f / (0x01000000u));
}

float2 Get2D(inout RNG rng)
{
    return float2(UniformFloat(rng), UniformFloat(rng));
}

float Get1D(inout RNG rng)
{
    return UniformFloat(rng);
}

RNG GetRNG(uint threadId)
{
    return _RNGs[threadId];
}

void WriteRNG(uint threadId, in RNG rng)
{
    _RNGs[threadId] = rng;
}
#endif
