#ifndef RESTIR_HLSL
#define RESTIR_HLSL

#define M 32

#include "../TraceRay.hlsl"

struct ReservoirSample
{
    float3 position;
    float targetFunction;
    float weightSum;
    float weight;
};

struct ReservoirOutSample
{
    float3 li;
    float3 wi;
    float2 padding;
};


//float3 EvaluatePHat(float3 worldPos, float3 surfaceNormal, float3 lightPos, float3 lightNormal, float3 wi, float3 lightEmission, inout RNG rng)
//{
//    float3 wi = lightPos - worldPos;
//    if (dot(wi, surfaceNormal) < 0.0f) 
//    {
//        return float3(0.0f);
//    }
//
//    
//}

float EvaluatePHat(float3 li, float3 f, float cos)
{
    return Luminance(li * f * cos);
}

#endif