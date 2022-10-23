#ifndef PATHINTEGRATOR_HLSL
#define PATHINTEGRATOR_HLSL


#include "light.hlsl"
#include "geometry.hlsl"
#include "bvhaccel.hlsl"
#include "materials.hlsl"

RWTexture2D<half4>  RayConeGBuffer;
int MAX_PATH;
int MIN_PATH;

float3 MIS_ShadowRay(Light light, Interaction isect, Material material, float lightSourcePdf, inout RNG rng)
{
    float3 wi;
    float lightPdf = 0;
    float3 samplePointOnLight;
    float3 ld = float3(0, 0, 0);
    float3 Li = SampleLightRadiance(light, isect, rng, wi, lightPdf, samplePointOnLight);
    lightPdf *= lightSourcePdf;
    //lightPdf = AreaLightPdf(light, isect, wi, _UniformSampleLight) * lightSourcePdf;
    if (!IsBlack(Li))
    {
        ShadowRay shadowRay = (ShadowRay)0;
        
        //shadowRay.pdf = triPdf;
        //shadowRay.lightPdf = lightPdf;

        float3 wiLocal = isect.WorldToLocal(wi);
        float3 woLocal = isect.WorldToLocal(isect.wo.xyz);
        float scatteringPdf = 0;

        float3 f = MaterialBRDF(material, isect, woLocal, wiLocal, scatteringPdf);
        if (!IsBlack(f) && scatteringPdf > 0)
        {
            float3 p0 = isect.p.xyz;
            float3 p1 = samplePointOnLight;

            bool shadowRayVisible = ShadowRayVisibilityTest(p0, p1, isect.normal);
 
            if (shadowRayVisible)
            {
                f *= abs(dot(wi, isect.normal));
                //sample psdf and compute the mis weight
                float weight = 
                    PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                ld = f * Li * weight / lightPdf;
                //ld = Li / lightPdf;
            }
        }
    }

    return ld;
}

float3 MIS_BSDF(Interaction isect, Material material, inout RNG rng, out PathVertex pathVertex)
{
    float3 ld = float3(0, 0, 0);
    //float3 woLocal = isect.WorldToLocal(isect.wo);
    //float3 wi;
    //float scatteringPdf = 0;
    pathVertex = (PathVertex)0;
    //float3 wiLocal;
    //float2 u = Get2D(rng);
    
    BSDFSample bsdfSample = SampleMaterialBRDF(material, isect, rng);
    float3 wi = isect.LocalToWorld(bsdfSample.wi);
    float scatteringPdf = bsdfSample.pdf;
    float3 f = bsdfSample.reflectance * abs(dot(wi, isect.normal));
    
    if (!IsBlack(f) && scatteringPdf > 0)
    {
        Ray ray = SpawnRay(isect.p.xyz, wi, isect.normal, FLT_MAX);
        //Interaction lightISect = (Interaction)0;
        HitInfo hitInfo = (HitInfo)0;
        bool found = ClosestHit(ray, hitInfo);
        //pathVertex.nextISect = lightISect; 
        pathVertex.found = found ? 1 : 0;  //can not use this expression or it will be something error. I don't know why.
        
        float3 li = 0;
        float lightPdf = 0;
        
        if (found)
        {
            ComputeSurfaceIntersection(hitInfo, -ray.direction, pathVertex.nextISect);
            int meshInstanceIndex = pathVertex.nextISect.meshInstanceID;
            MeshInstance meshInstance = MeshInstances[meshInstanceIndex];
            int hitLightIndex = meshInstance.GetLightIndex();
            if (hitLightIndex >= 0)
            {
                Light hitLight = lights[hitLightIndex];
                float lightSourcePmf = LightSourcePmf(hitLightIndex);
                lightPdf = AreaLightPdf(hitLight) * lightSourcePmf;
                if (lightPdf > 0)
                {
                    li = Light_Le(wi, hitLight);
                }
            }
            //if (meshInstance.GetLightIndex() == lightIndex)
            //{
            //    lightPdf = AreaLightPdf(light, pathVertex.nextISect) * lightSourcePdf;

            //    if (lightPdf > 0)
            //    {
            //        li = Light_Le(wi, light);
            //    }
            //}
        }
        else if (_EnvLightIndex >= 0)//(light.type == EnvLightType)
        {
            Light envLight = lights[_EnvLightIndex];
            li = Light_Le(wi, envLight);
            //if (light.type != EnvLightType)
            {
                float lightSourcePmf = LightSourcePmf(_EnvLightIndex);
                lightPdf = EnvLightLiPdf(wi) * lightSourcePmf;
            }
        }

        float weight = bsdfSample.IsSpecular() ? 1 : PowerHeuristic(1, scatteringPdf, 1, lightPdf);
        //if (!bsdfSample.IsSpecular())
        //    weight = PowerHeuristic(1, scatteringPdf, 1, lightPdf);
        ld = f * li * weight / scatteringPdf;
    }

    pathVertex.wi = wi;
    pathVertex.bsdfVal = f;
    pathVertex.bsdfPdf = scatteringPdf;

    return ld;
}

Light SampleLightSource(inout RNG rng, out float lightSourcePdf, out int lightIndex)
{

    //some error happen in SampleLightSource
    float u = Get1D(rng);
    DistributionDiscript discript = DistributionDiscripts[0];
    lightIndex = SampleLightSource(u, discript, Distributions1D, lightSourcePdf);
    //lightIndex = 0;
    //lightSourcePdf = 0.5;
    Light light = lights[lightIndex];
    return light;
}

float3 EstimateDirectLighting(Interaction isect, inout RNG rng, out PathVertex pathVertex, bool breakPath)
{
    breakPath = false;
    //PathRadiance pathRadiance = (PathRadiance)0;
    //pathRadiance.beta = float3(1, 1, 1);
    float lightSourcePdf = 0;
    Material material = materials[isect.materialID];
    int lightIndex = 0;
    Light light = SampleLightSource(rng, lightSourcePdf, lightIndex);

    pathVertex = (PathVertex)0;
    float3 ld = MIS_ShadowRay(light, isect, material, lightSourcePdf, rng);
    ld += MIS_BSDF(isect, material, rng, pathVertex);

    if (pathVertex.bsdfPdf == 0 || MaxValue(pathVertex.bsdfVal) == 0 || MaxValue(ld) == 0)
    {
        breakPath = true;
    }

    return ld;
}

float3 PathLi(Ray ray, uint2 id, inout RNG rng)
{
	float3 li = 0;
    float3  beta = 1;
    Interaction isectLast;
    PathVertex pathVertex = (PathVertex)0;
    Interaction isect;
	for (int bounces = 0; bounces < MAX_PATH; bounces++)
	{
        bool foundIntersect = false;
        if (bounces == 0)
        {
            HitInfo hitInfo = (HitInfo)0;
            foundIntersect = ClosestHit(ray, hitInfo);
            if (foundIntersect)
            {
                ComputeSurfaceIntersection(hitInfo, -ray.direction, isect);
            }
        }
        else
        {
            foundIntersect = pathVertex.found == 1;
        }

        //PathRadiance pathRadiance = pathRadiances[workIndex];
        if (foundIntersect)
        {
            int meshInstanceIndex = isect.meshInstanceID;
            MeshInstance meshInstance = MeshInstances[meshInstanceIndex];
            int lightIndex = meshInstance.GetLightIndex();


            half4 surfaceBeta = RayConeGBuffer[id.xy];
            RayCone preCone;
            preCone.width = surfaceBeta.z;
            preCone.spreadAngle = surfaceBeta.y;
            if (bounces == 0)
            {
                //half2 surfaceBeta = RayConeGBuffer[id.xy];
                //isect.spreadAngle = cameraConeSpreadAngle;
                surfaceBeta.y = cameraConeSpreadAngle + surfaceBeta.x;
                isect.coneWidth = cameraConeSpreadAngle * isect.hitT;
            }
            else
            {
                RayCone rayCone = ComputeRayCone(preCone, isect.hitT, surfaceBeta.r);
                //isect.spreadAngle = rayCone.spreadAngle;
                isect.coneWidth = rayCone.width;
            }

            //isect.p.w = 1;
            if (lightIndex >= 0 && bounces == 0)
            {
                Light light = lights[lightIndex];
                li += light.radiance * beta;
                //color = light.radiance;
                //isect.p.w = 0;
            }

            bool breakPath = false;
            float3 ld = EstimateDirectLighting(isect, rng, pathVertex, breakPath);
            //li += beta * SampleLight(isect, wi, rng, pathBeta, ray);
            li += ld * beta;
            //return li;
            if (breakPath)
                break;
            
            float3 throughput = pathVertex.bsdfVal / pathVertex.bsdfPdf;
            beta *= throughput;

            //Russian roulette
            if (bounces > MIN_PATH)
            {
                float q = max(0.05, 1 - MaxComponent(beta));
                if (Get1D(rng) < q)
                {
                    break;
                }
                else
                    beta /= 1 - q;
            }

            isectLast = isect;
        }
        else
        {
            //sample enviroment map
            if (bounces == 0 && _EnvLightIndex >= 0)
            {
                li += beta * EnviromentLightLe(ray.direction);
            }
            break;
        }

        //ray = SpawnRay(isect.p.xyz, pathVertex.wi, isect.normal, FLT_MAX);
        isect = pathVertex.nextISect;
	}
	return li;
}

#endif
