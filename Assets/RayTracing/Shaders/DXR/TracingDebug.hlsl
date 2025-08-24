#ifndef TRACING_DEBUG
#define TRACING_DEBUG

#include "materials.hlsl"
#include "sampleAreaLights.hlsl"

#define NormalView 1
#define DepthView 2
#define MipmapView 3
#define GBuffer    4
#define ShadowRayView  5
#define FresnelView 6
#define EnvmapUVView 7
#define RNGView 8


half3 TracingDebug(uint id, RayDesc ray, int view, float2 rayCone, float cameraConeSpreadAngle, inout RNG rng)
{
    half3 color = 0;
    PathPayload payLoad;
    payLoad.instanceID = -1;
    payLoad.threadID = id;
    payLoad.hitResult = 0;
    HitSurface hitCur;
    Material material;

    //payLoad.rayCone = rayCone;

    TraceRay(_AccelerationStructure, 0, 0xFF, 0, 1, 0, ray, payLoad);
    bool foundIntersect = payLoad.hitResult > 0;
    if (foundIntersect)
    {
        hitCur = payLoad.hitSurface;
        material = _Materials[payLoad.threadID];

        switch (view)
        {
        case NormalView:
            color = hitCur.normal * 0.5 + 0.5;
            break;
        case DepthView:
            color = half3(hitCur.hitT, hitCur.hitT, hitCur.hitT) / _CameraFarDistance;
            break;
        case MipmapView:
            //TextureSampleInfo texLod = (TextureSampleInfo)0;
            //texLod.cosine = dot(hitCur.wo, hitCur.normal);
            //texLod.coneWidth = hitCur.coneWidth;
            //texLod.screenSpaceArea = hitCur.screenSpaceArea;
            //texLod.uvArea = hitCur.uvArea;
            //float mipmapLevel = ComputeTextureLOD(texLod) / log2(512);
            //color = lerp(half3(0, 0, 1), half3(1, 0, 0), mipmapLevel * 2);
            color = 0;
            break;
        case GBuffer:
            color.rg = rayCone;
            break;
        case ShadowRayView:
        {
            float lightSourcePdf = 1.0;
            LightSource lightSource = SelectLightSource(Get1D(rng), lightSourcePdf);
            float3 wi;
            float lightPdf = 0;
            float3 samplePointOnLight;
            float3 ld = float3(0, 0, 0);
            float3 lightNormal;
            float3 Li = 0;
            if (lightSource.lightType == AreaLightType)
            {
                AreaLight light = _Lights[lightSource.lightIndex];
                Li = SampleLightRadiance(light, hitCur.position, rng, wi, lightPdf, samplePointOnLight, lightNormal);
            }
            else
            {
                Li = ImportanceSampleEnviromentLight(Get2D(rng), lightPdf, wi);
                lightNormal = -wi;
                samplePointOnLight = hitCur.position + wi * _CameraFarDistance; // far away point in the direction of the light
            }
            lightPdf *= lightSourcePdf;
            if (lightPdf > 0)
            {
                bool visible = TestRayVisibility(hitCur.position, samplePointOnLight, hitCur.normal, ShadowEpsilon);
                color = visible ? Li : float3(0, 0, 0);
                float3 dpdu = float3(1, 0, 0);
                float3 dpdv = float3(0, 1, 0);
                CoordinateSystem(hitCur.normal, dpdu, dpdv);
                float3 tangent = normalize(dpdu);
                float3 bitangent = normalize(cross(tangent, hitCur.normal));
                float3 wiLocal = hitCur.WorldToLocal(wi, tangent, bitangent);
                float3 woLocal = hitCur.wo;
                float scatteringPdf = 0;
                float3 f = MaterialBRDF(material, woLocal, wiLocal, scatteringPdf);
                if (scatteringPdf == 0)
                {
                    color = float3(1, 0, 0);
                }
                else
                {
                    float3 beta = f * abs(dot(wi, hitCur.normal)) / lightPdf;
                    color = Li * beta;
                }
            }
            //else
            //{
            //    color = float3(0, 1, 0);
            //}
            //color = wi * 0.5 + 0.5;
        }
        break;
        case FresnelView:
        {
            //Light light = lights[0];
            //color = FresnelColor(isect, material, rng);
            color = 0;
        }
        break;
        case EnvmapUVView:
        {
            color = 0;
        }
        break;
        case RNGView:
        {
            float2 u = Get2D(rng);
            color = float3(u, Get1D(rng));
        }
        break;
        }
    }
    return color;
}

#endif