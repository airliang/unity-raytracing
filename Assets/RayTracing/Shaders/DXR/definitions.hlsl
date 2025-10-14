#ifndef DEFINITION_HLSL
#define DEFINITION_HLSL

#define CBUFFER_START(name) cbuffer name {
#define CBUFFER_END };

CBUFFER_START(CameraBuffer)
float4x4 _InvViewProj;
float4x4 _PreframeInvViewProj;
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
float4x4 _PreInvCameraViewProj;
CBUFFER_END


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
    float2 padding;

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
    uint isShadowRay;
    uint threadID;
    //float coneWidth;
    //float2 barycentrics;
    //float hitT;
    RayCone rayCone;
    //HitSurface hitSurface;
    //Material  material;
};

struct ShadowPayload
{
    bool hit;
};

struct ReservoirSample
{
    float3 position;
    float targetFunction;
    float weightSum;
    float weight;
    uint  numStreams;
    uint  lightIndex;
    float4 targetFunctionFull;
};

SamplerState s_point_clamp_sampler;
SamplerState s_point_repeat_sampler;
SamplerState s_linear_clamp_sampler;
SamplerState s_linear_repeat_sampler;
SamplerState s_trilinear_clamp_sampler;
SamplerState s_trilinear_repeat_sampler;
#endif
