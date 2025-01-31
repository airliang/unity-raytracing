#ifndef DXRCOMMON_HLSL
#define DXRCOMMON_HLSL

#include "mathdef.hlsl"

#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };

CBUFFER_START(CameraBuffer)
float4x4 _InvCameraViewProj;
float4x4 _RasterToCamera;
float4x4 _CameraToWorld;
float3   _WorldSpaceCameraPos;
float    _CameraFarDistance;
float    _LensRadius;
float    _FocalLength;
int _FrameIndex;
int _MaxDepth;
int _LightsNum;
CBUFFER_END

struct CameraSample
{
    float2 pFilm;
};

struct RNG
{
    uint state;
};

#define HIT_MISS 0
#define HIT_MESH 1
#define HIT_LIGHT 2

struct RayIntersection
{
    float4 color;
    float3 direction;
    float3 beta;
    RNG rng;
    int bounce;
    int hitResult;
    int instanceID;
    int primitiveID;
};

struct HitSurface
{
    float3 position;
    float2 uv;
    float3 normal;
    float3 tangent;  //the same as pbrt's ss(x)
    float3 bitangent; //the same as pbrt's ts(y)
    float3 wo;
    float  primArea;
    float  coneWidth;     //ray cone width at this surface point
    float  screenSpaceArea;
    float  uvArea;
    float  mip;
    float2  padding;

    float3 WorldToLocal(float3 v)
    {
        return float3(dot(tangent, v), dot(bitangent, v), dot(normal, v));
    }

    float3 LocalToWorld(float3 v)
    {
        return tangent * v.x + bitangent * v.y + normal * v.z;
    }
};

typedef BuiltInTriangleIntersectionAttributes AttributeData;

RaytracingAccelerationStructure _AccelerationStructure;

RWStructuredBuffer<RNG>    _RNGs;

//同心圆盘采样
float2 ConcentricSampleDisk(float2 u)
{
    //mapping u to [-1,1]
    float2 u1 = float2(u.x * 2.0f - 1, u.y * 2.0f - 1);

    if (u1.x == 0 && u1.y == 0)
        return float2(0, 0);

    //r = x
    //θ = y/x * π/4
    //最后返回x,y
    //x = rcosθ, y = rsinθ
    float theta, r;
    if (abs(u1.x) > abs(u1.y))
    {
        r = u1.x;
        theta = u1.y / u1.x * PI_OVER_4;
    }
    else
    {
        //这里要反过来看，就是把视野选择90度
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

SamplerState s_point_clamp_sampler;
SamplerState s_point_repeat_sampler;
SamplerState s_linear_clamp_sampler;
SamplerState s_linear_repeat_sampler;
SamplerState s_trilinear_clamp_sampler;
SamplerState s_trilinear_repeat_sampler;
#endif
