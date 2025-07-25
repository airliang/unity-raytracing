#ifndef DXRCOMMON_HLSL
#define DXRCOMMON_HLSL

#include "mathdef.hlsl"

#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };

CBUFFER_START(CameraBuffer)
float4x4 _InvCameraViewProj;
float4x4 _RasterToCamera;
float4x4 _CameraToWorld;
float4x4 _WorldToRaster;
float3   _CameraPosWS;
float    _CameraFarDistance;
float    _LensRadius;
float    _FocalLength;
float _CameraConeSpreadAngle;
int _FrameIndex;
int _MinDepth;
int _MaxDepth;
int _LightsNum;
int _DebugView;
int _EnvironmentMapEnable;
float _EnvironmentLightPmf;
float4 _ScreenSize;
CBUFFER_END

#define GLOBAL_RESOURCE(type, name, reg) type name : register(reg, space1);

#define _ENVIRONMENT_MAP_ENABLE _EnvironmentMapEnable == 1

struct CameraSample
{
    float2 pFilm;
};

struct RNG
{
    uint state;
};

struct RayCone
{
    float spreadAngle;
    float width;
};

#define HIT_MISS 0
#define HIT_MESH 1
#define HIT_LIGHT 2

struct Material
{
    int materialType;
    float3 kd;
    float3 ks;
    float3 transmission;
    float metallic;
    float specular;
    float roughness;
    float anisotropy;
    float3 eta;
    float3 k;             //metal material absorption
    float fresnelType;
};

struct Interaction
{
    float3 position;
    float2 uv;
    float3 normal;
    float3 tangent;  //the same as pbrt's ss(x)
    float3 bitangent; //the same as pbrt's ts(y)
    float3 wo;
    //float  primArea;
    float  uvArea;    
    float  screenSpaceArea;

    float3 WorldToLocal(float3 v)
    {
        return float3(dot(tangent, v), dot(bitangent, v), dot(normal, v));
    }

    float3 LocalToWorld(float3 v)
    {
        return tangent * v.x + bitangent * v.y + normal * v.z;
    }
};

struct HitSurface
{
    float3 position;
    //float2 uv;
    float3 normal;
    //float3 tangent;  //the same as pbrt's ss(x)
    //float3 bitangent; //the same as pbrt's ts(y)
    float3 wo;   //local space
    //float  primArea;
    //float  coneWidth;     //ray cone width at this surface point
    //float  screenSpaceArea;
    //float  uvArea;
    //float  mip;
    float  hitT;

    float3 WorldToLocal(float3 v, float3 tangent, float3 bitangent)
    {
        //float3 dpdu = float3(1, 0, 0);
        //float3 dpdv = float3(0, 1, 0);
        //CoordinateSystem(normal, dpdu, dpdv);
        //float3 tangent = normalize(dpdu.xyz);
        //float3 bitangent = normalize(cross(surface.tangent.xyz, worldNormal));
        return float3(dot(tangent, v), dot(bitangent, v), dot(normal, v));
    }

    float3 LocalToWorld(float3 v, float3 tangent, float3 bitangent)
    {
        //float3 dpdu = float3(1, 0, 0);
        //float3 dpdv = float3(0, 1, 0);
        //CoordinateSystem(normal, dpdu, dpdv);
        //float3 tangent = normalize(dpdu.xyz);
        //float3 bitangent = normalize(cross(surface.tangent.xyz, worldNormal));
        return tangent * v.x + bitangent * v.y + normal * v.z;
    }
};

struct PathPayload
{
    int hitResult;
    uint instanceID;
    //uint bounce;
    uint threadID;
    //float coneWidth;
    //float2 barycentrics;
    //float hitT;
    RayCone rayCone;
    HitSurface hitSurface;
    //Material  material;
};

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

SamplerState s_point_clamp_sampler;
SamplerState s_point_repeat_sampler;
SamplerState s_linear_clamp_sampler;
SamplerState s_linear_repeat_sampler;
SamplerState s_trilinear_clamp_sampler;
SamplerState s_trilinear_repeat_sampler;
#endif
