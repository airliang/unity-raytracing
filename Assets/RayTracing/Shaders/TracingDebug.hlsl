#ifndef TRACING_DEBUG
#define TRACING_DEBUG

#include "sampler.hlsl"
#include "bvhaccel.hlsl"
#include "materials.hlsl"
#include "light.hlsl"

#define NormalView 1
#define DepthView 2
#define MipmapView 3
#define GBuffer    4
#define ShadowRayView  5
#define FresnelView 6
#define EnvmapUVView 7

int debugView;


uniform float cameraFar;



float3 MaterialFresnelShadowRay(Light light, Material material, Interaction isect, inout RNG rng)
{
    float3 wi;
    float lightPdf = 0;
    float3 samplePointOnLight;
    float3 Li = SampleLightRadiance(light, isect, rng, wi, lightPdf, samplePointOnLight);
    ShadowRay shadowRay = (ShadowRay)0;
    if (lightPdf > 0)
    {
        float3 p0 = isect.p.xyz;
        float3 p1 = samplePointOnLight;
        //shadowRay.pdf = triPdf;
        //shadowRay.lightPdf = lightPdf;
        //float3 Li = light.radiance;
        //shadowRay.lightNormal = lightPointNormal;
        //float3 wi = normalize(shadowRay.p1 - shadowRay.p0);

        bool shadowRayVisible = ShadowRayVisibilityTest(p0, p1, isect.normal);
        if (!shadowRayVisible)
        {
            return 0;
        }
    }
    float3 wo = isect.WorldToLocal(isect.wo.xyz);

    ShadingMaterial shadingMaterial = (ShadingMaterial)0;
    if (shadingMaterial.materialType == Disney)
    {

    }
    else
    {
        TextureSampleInfo texLod = (TextureSampleInfo)0;
        texLod.cosine = dot(isect.wo, isect.normal);
        texLod.coneWidth = isect.coneWidth;
        texLod.screenSpaceArea = isect.screenSpaceArea;
        texLod.uvArea = isect.uvArea;
        texLod.uv = isect.uv.xy;
        UnpackShadingMaterial(material, shadingMaterial, texLod);
        int nComponent = 0;
        if (shadingMaterial.materialType == Plastic)
        {
            BxDFPlastic bxdfPlastic;
            ComputeBxDFPlastic(shadingMaterial, bxdfPlastic);
            float pdfMicroReflection = 0;
            return bxdfPlastic.Fresnel(wo, wi);//MicrofacetReflectionF(wo, wi, bxdf, pdfMicroReflection);
        }
        else if (shadingMaterial.materialType == Metal)
        {
            BxDFMetal bxdfMetal;
            ComputeBxDFMetal(shadingMaterial, bxdfMetal);
            float3 wh = wi + wo;
            wh = normalize(wh);
            wh = isect.LocalToWorld(wh);
            return bxdfMetal.Fresnel(wo, wi);  //MicrofacetReflectionF(wo, wi, bxdf, pdf);
        }
        else
        {
            return 0;
        }
    }
    return 0;
}

float2 ImportanceSampleEnvmapUV(float2 u)
{
    if (_EnvMapEnable)
    {
        DistributionDiscript discript = (DistributionDiscript)0;
        discript.start = 0;
        discript.num = (int)envMapDistributionSize.y;
        discript.unum = (int)envMapDistributionSize.x;
        discript.domain = float4(0, 1, 0, 1);
        float mapPdf = 0;

        float2 uv = Sample2DContinuous(u, discript, EnvmapMarginals, EnvmapConditions, EnvmapConditionFuncInts, mapPdf);
        return uv;
    }
    else
        return u;
}


float3 MaterialFresnel(Material material, Interaction isect, inout RNG rng)
{
    float3 wo = isect.WorldToLocal(isect.wo.xyz);

    ShadingMaterial shadingMaterial = (ShadingMaterial)0;
    TextureSampleInfo texLod = (TextureSampleInfo)0;
    texLod.cosine = dot(isect.wo, isect.normal);
    texLod.coneWidth = isect.coneWidth;
    texLod.screenSpaceArea = isect.screenSpaceArea;
    texLod.uvArea = isect.uvArea;
    texLod.uv = isect.uv.xy;
    UnpackShadingMaterial(material, shadingMaterial, texLod);

    switch (shadingMaterial.materialType)
    {
    case Disney:
        return 0;
    case Matte:
    {
        BxDFLambertReflection bxdfLambert;
        ComputeBxDFLambertReflection(shadingMaterial, bxdfLambert);
        float2 u = Get2D(rng);
        return 0;
    }
    case Plastic:
    {
        BxDFPlastic bxdfPlastic;
        ComputeBxDFPlastic(shadingMaterial, bxdfPlastic);
        float2 u = Get2D(rng);
        float3 wh = bxdfPlastic.Sample_wh(u, wo);
        float3 wi = reflect(-wo, wh);
        return bxdfPlastic.Fresnel(wo, wi);
    }
    case Metal:
    {
        BxDFMetal bxdf;
        ComputeBxDFMetal(shadingMaterial, bxdf);
        
        float2 u = Get2D(rng);
        float3 wh = normalize(bxdf.Sample_wh(u, wo));
        float3 wi = normalize(reflect(-wo, wh));
        float scatteringPdf = 0;
        return bxdf.Fresnel(wo, wi);
        //return SampleMetal(shadingMaterial, wo, wi, rng, scatteringPdf);//SampleMaterialBRDF(material, isect, wo, wi, scatteringPdf, rng);
    }
    case Mirror:
        return 0;
    case Glass:
        return 0;
    default:
    {
        return 0;
    }
    }

    return 0;
}

float3 SampleShadowRayRadiance(Light light, Interaction isect, out float3 wi, out float lightPdf, inout RNG rng)
{
	ShadowRay shadowRay = (ShadowRay)0;

	lightPdf = 0;
	float3 samplePointOnLight;
	float3 Li = SampleLightRadiance(light, isect, rng, wi, lightPdf, samplePointOnLight);

    if (lightPdf > 0)
    {
        float3 p0 = isect.p.xyz;
        float3 p1 = samplePointOnLight;
        //shadowRay.pdf = triPdf;
        //shadowRay.lightPdf = lightPdf;
        //float3 Li = light.radiance;
        //shadowRay.lightNormal = lightPointNormal;
        //float3 wi = normalize(shadowRay.p1 - shadowRay.p0);

        bool shadowRayVisible = ShadowRayVisibilityTest(p0, p1, isect.normal);
        if (shadowRayVisible)
        {
            shadowRay.radiance = Li;
        }
    }

	return shadowRay.radiance;
}

float3 FresnelColor(Interaction isect, Material material, inout RNG rng)
{
    return MaterialFresnel(material, isect, rng);
}

half3 TracingDebug(uint2 id, Ray ray, int view, float2 rayCone, float cameraConeSpreadAngle, inout RNG rng)
{
    HitInfo hitInfo = (HitInfo)0;
    bool foundIntersect = ClosestHit(ray, hitInfo);//IntersectBVH(ray, isect);
    half3 color = half3(0, 0, 0);
    if (foundIntersect)
    {
        Interaction isect = (Interaction)0;
        ComputeSurfaceIntersection(hitInfo, -ray.direction, isect);
        int meshInstanceIndex = isect.meshInstanceID;
        MeshInstance meshInstance = MeshInstances[meshInstanceIndex];

        //isect.spreadAngle = cameraConeSpreadAngle;
        isect.coneWidth = cameraConeSpreadAngle * isect.hitT;
        Material material = materials[isect.materialID];


        switch (view)
        {
        case NormalView:
            color = isect.normal * 0.5 + 0.5;
            break;
        case DepthView:
            color = half3(isect.hitT, isect.hitT, isect.hitT) / cameraFar;
            break;
        case MipmapView:
            TextureSampleInfo texLod = (TextureSampleInfo)0;
            texLod.cosine = dot(isect.wo, isect.normal);
            texLod.coneWidth = isect.coneWidth;
            texLod.screenSpaceArea = isect.screenSpaceArea;
            texLod.uvArea = isect.uvArea;
            float mipmapLevel = ComputeTextureLOD(texLod) / log2(512);
            color = lerp(half3(0, 0, 1), half3(1, 0, 0), mipmapLevel * 2);
            break;
        case GBuffer:
            color.rg = rayCone;
            break;
        case ShadowRayView:
        {
            float u = Get1D(rng);
            float lightSourcePdf = 0;
            int lightIndex = SampleLightSource(u, DistributionDiscripts[0], Distributions1D, lightSourcePdf);
            Light light = lights[lightIndex];
            float3 wi;
            float lightPdf = 0;
            float3 shadowRayRadiance = SampleShadowRayRadiance(light, isect, wi, lightPdf, rng);
            if (lightPdf > 0)
            {
                float3 woLocal = isect.WorldToLocal(isect.wo);
                //float3 wi;
                //float scatteringPdf = 0;
                float3 wiLocal = isect.WorldToLocal(wi);

                Material material = materials[isect.materialID];
                float scatteringPdf = 0;
                float3 f = MaterialBRDF(material, isect, woLocal, wiLocal, scatteringPdf);
                if (scatteringPdf == 0)
                {
                    color = float3(1, 0, 0);
                }
                else
                {
                    float beta = f * abs(dot(wi, isect.normal)) / lightPdf;
                    color = shadowRayRadiance * beta;
                }
            }
            else
            {
                color = float3(0, 1, 0);
            }
            //color = wi * 0.5 + 0.5;
        }
        break;
        case FresnelView:
        {
            Light light = lights[0];
            color = FresnelColor(isect, material, rng);
        }
        break;
        case EnvmapUVView:
        {
            DistributionDiscript lightSourceDiscript = DistributionDiscripts[0];
            for (int i = 0; i < lightSourceDiscript.num; ++i)
            {
                Light light = lights[i];
                if (light.type == EnvLightType)
                {
                    float lightSourcePdf = DiscretePdf(0, lightSourceDiscript, Distributions1D);
                    float2 u = Get2D(rng);
                    float3 wi;
                    float pdf = 0;
                    //float lightSourcePdf1 = DiscretePdf(1, lightSourceDiscript, Distributions1D);
                    color = ImportanceSampleEnviromentLight(u, pdf, wi);
                    //color = UniformSampleEnviromentLight(u, pdf, wi);
                    color = wi;
                    //color.rg = ImportanceSampleEnvmapUV(u);
                    break;
                }
            }
        }
        break;
        }
    }
    return color;
}

#endif