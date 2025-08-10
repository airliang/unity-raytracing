#ifndef SAMPLELIGHTS_HLSL
#define SAMPLELIGHTS_HLSL

#include "distributions.hlsl"
#include "geometry.hlsl"

struct AreaLight
{
    int distributionDiscriptIndex;   //triangle area distribution
    int instanceID;
    int triangleLightOffset;
    int triangleLightsNum;
    float3 radiance;
    float  area;    //for area light;
    float4x4 localToWorld;
};

struct TriangleLight
{
    int triangleIndex;
    int lightIndex;
    float area;
    float padding;
};

StructuredBuffer<AreaLight> _Lights;
StructuredBuffer<TriangleLight> _TriangleLights;
//StructuredBuffer<float2> _LightDistributions1D;
//StructuredBuffer<DistributionDiscript> _LightDistributionDiscripts;

StructuredBuffer<int3> _LightTriangles;
StructuredBuffer<Vertex> _LightVertices;

float3 SampleTriangleLight(float3 p0, float3 p1, float3 p2, float2 u, float3 litPoint, AreaLight light, out float3 wi, out float3 position, out float attenuation, out float3 lightNormal)
{
    //float3 Li = 0;
    float3 lightPointNormal;
    float triPdf = 0;
    position = SamplePointOnTriangle(p0, p1, p2, u, lightPointNormal, triPdf);
    lightNormal = lightPointNormal;
    attenuation = 1.0f;
 
    //wi = position - litPoint;
    //float wiLength = length(wi);
    //wi = normalize(wi);
    //float cos = dot(lightPointNormal, -wi);
    //float absCos = abs(cos);
    //attenuation = wiLength * wiLength / absCos;
    //if (isinf(attenuation) || wiLength == 0)
    //{
    //    attenuation = 0;
    //    return 0;
    //}

    return light.radiance;//cos > 0 ? light.radiance : 0;
}

int SampleTriangleIndexOfLightPoint(float u, DistributionDiscript discript, StructuredBuffer<float2> distributions, out float pdf)
{
    //get light mesh triangle index
    int index = Sample1DDiscrete(u, discript, distributions, pdf);
    return index;
}

float3 SampleLightRadiance(AreaLight light, float3 intersectPoint, inout RNG rng,
    out float3 wi, out float lightPdf, out float3 lightPoint, out float3 lightNormal)
{
    float u = Get1D(rng);
    float triPdf = 0;
    lightPdf = 0;

    int triangleLightIndex = min((int)(u * light.triangleLightsNum), light.triangleLightsNum - 1);
    TriangleLight triLight = _TriangleLights[triangleLightIndex + light.triangleLightOffset];
    uint3 triangleIndices = _LightTriangles[triLight.triangleIndex];

    float3 p0 = _LightVertices[triangleIndices.x].position;
    float3 p1 = _LightVertices[triangleIndices.y].position;
    float3 p2 = _LightVertices[triangleIndices.z].position;
    //convert to worldpos

    p0 = mul(light.localToWorld, float4(p0, 1)).xyz;
    p1 = mul(light.localToWorld, float4(p1, 1)).xyz;
    p2 = mul(light.localToWorld, float4(p2, 1)).xyz;

    //float triangleArea = triLight.area;
    //lightPdf = triangleArea / light.area;
    float attenuation = 1.0;
    float3 Li = SampleTriangleLight(p0, p1, p2, Get2D(rng), intersectPoint, light, wi, lightPoint, attenuation, lightNormal);
    wi = lightPoint - intersectPoint;
    attenuation = dot(wi, wi);
    wi = normalize(wi);
    float cos = dot(lightNormal, -wi);
    float absCos = abs(cos);
    if (isinf(attenuation) || absCos == 0)
    {
        lightPdf = 0;
        return 0;
    }
    Li /= attenuation;
    
    lightPdf = /*attenuation*/1.0 / absCos;
    return cos > 0 ? Li : 0;
}

int ImportanceSampleLightSource(float u, DistributionDiscript discript, StructuredBuffer<float2> discributions, out float pmf)
{
    return Sample1DDiscrete(u, discript, discributions, pmf);
}

int UniformSampleLightSource(float u, int nLightsNum, out float pmf)
{
    int lightIndex = min((int)(u * nLightsNum), nLightsNum - 1);
    pmf = 1.0 / nLightsNum;
    return lightIndex;
}

float LightSourcePmf(int lightIndex)
{
    return (1.0 / _LightsNum) / (1.0 - _EnvironmentLightPmf);
}

float AreaLightPdf(AreaLight light)
{
    float lightPdf = 1.0 / light.area;
    return lightPdf;
}
#endif
