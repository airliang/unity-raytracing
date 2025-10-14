#ifndef RESTIR_HLSL
#define RESTIR_HLSL

#define M 32
#define M_BSDF 0

#include "../TraceRay.hlsl"

float EvaluatePHat(float3 li, float3 f, float3 surfaceNormal, float3 wi, out float4 pHatFull)
{
    pHatFull = 0;
    float cos = dot(surfaceNormal, wi);
    if (cos < 0)
        return 0;
    pHatFull = float4(li * f * cos, 1);
    return Luminance(pHatFull);
}

float3 EvaluatePHat(HitSurface hitSurface, float3 lightPos, float3 lightNormal, float3 li, float3 wi, Material material)
{
    float cos = dot(hitSurface.normal, wi);
    if (cos < 0)
        return 0;
    float3 dpdu = float3(1, 0, 0);
    float3 dpdv = float3(0, 1, 0);
    CoordinateSystem(hitSurface.normal, dpdu, dpdv);
    float3 tangent = normalize(dpdu);
    float3 bitangent = normalize(cross(tangent, hitSurface.normal));
    float3 woLocal = hitSurface.wo;
    float3 wiLocal = hitSurface.WorldToLocal(wi, tangent, bitangent);
    float scatteringPdf = 0;
    float3 f = MaterialBRDF(material, woLocal, wiLocal, scatteringPdf);
    float3 w = lightPos - hitSurface.position;
    float attenuation = dot(w, w);

    return li * f * abs(dot(wi, hitSurface.normal)) / attenuation;
}

#endif