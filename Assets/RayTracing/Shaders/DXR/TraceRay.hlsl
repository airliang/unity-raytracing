#ifndef TRACERAY_HLSL
#define CAMERARAY_HLSL

#include "DXRCommon.hlsl"
#include "FilterImportanceSampling.hlsl"
#include "UnityRayTracingMeshUtils.cginc"
#include "materials.hlsl"
#include "sampleAreaLights.hlsl"

float origin() { return 1.0f / 32.0f; }
float float_scale() { return 1.0f / 65536.0f; }
float int_scale() { return 256.0f; }

// Normal points outward for rays exiting the surface, else is flipped.
float3 offset_ray(const float3 p, const float3 n)
{
    int3 of_i = int3(int_scale() * n.x, int_scale() * n.y, int_scale() * n.z);

    float3 p_i = float3(
        asfloat(asint(p.x) + ((p.x < 0) ? -of_i.x : of_i.x)),
        asfloat(asint(p.y) + ((p.y < 0) ? -of_i.y : of_i.y)),
        asfloat(asint(p.z) + ((p.z < 0) ? -of_i.z : of_i.z)));

    return float3(abs(p.x) < origin() ? p.x + float_scale() * n.x : p_i.x,
        abs(p.y) < origin() ? p.y + float_scale() * n.y : p_i.y,
        abs(p.z) < origin() ? p.z + float_scale() * n.z : p_i.z);
}

float PowerHeuristic(int nf, float fPdf, int ng, float gPdf)
{
    float f = nf * fPdf;
    float g = ng * gPdf;
    return (f * f) / (f * f + g * g);
}

float BalanceHeuristic(int nf,
    float f_PDF,
    int ng,
    float g_PDF)
{
    return (nf * f_PDF) / (nf * f_PDF + ng * g_PDF);
}

struct PathVertex
{
    float3 wi;
    float3 bsdfVal;
    float  bsdfPdf;
};

CameraSample GetCameraSample(float2 pRaster, float2 u)
{
    CameraSample camSample;
    float2 filter = ImportanceFilterSample(u);
    camSample.pFilm = pRaster + float2(0.5, 0.5) + filter;
    return camSample;
}

inline void GenerateCameraRay(out float3 origin, out float3 direction)
{
    float2 xy = DispatchRaysIndex().xy + 0.5f; // center in the middle of the pixel.
    float2 screenPos = xy / DispatchRaysDimensions().xy * 2.0f - 1.0f;

    // Un project the pixel coordinate into a ray.
    float4 world = mul(_InvCameraViewProj, float4(screenPos, 0, 1));

    world.xyz /= world.w;
    origin = _WorldSpaceCameraPos.xyz;
    direction = normalize(world.xyz - origin);
}

RayDesc GenerateRay(uint2 id, inout RNG rng)
{
    float2 u = Get2D(rng);

    CameraSample cSample = GetCameraSample(float2(id.x, id.y), u);
    float3 pFilm = float3(cSample.pFilm, 0);
    float4 nearplanePoint = mul(_RasterToCamera, float4(pFilm, 1));
    nearplanePoint /= nearplanePoint.w;
    float3 orig = float3(0, 0, 0);
    float3 direction = normalize(nearplanePoint.xyz);
    if (_LensRadius > 0)
    {
        //sample the points on the lens
        float2 uLens = Get2D(rng);
        float2 pLens = ConcentricSampleDisk(uLens) * _LensRadius;
        float ft = abs(_FocalLength / direction.z);
        float3 focusPoint = orig + direction * ft;
        orig = float3(pLens, 0);
        direction = normalize(focusPoint - orig);
    }
    RayDesc ray;
    ray.Origin = mul(_CameraToWorld, float4(orig, 1)).xyz;
    ray.Direction = mul(_CameraToWorld, float4(direction, 0)).xyz;
    ray.TMax = _CameraFarDistance;
    ray.TMin = 0;
    return ray;
}

RayDesc SpawnRay(float3 p, float3 direction, float3 normal, float tMax)
{
    RayDesc ray;
    float s = sign(dot(normal, direction));
    normal *= s;
    ray.Origin = offset_ray(p, normal);
    ray.TMax = tMax;
    ray.Direction = direction;
    ray.TMin = 0;
    return ray;
}

HitSurface GetHitSurface(uint primIndex, float3 wo, AttributeData attributeData)
{
    HitSurface surface = (HitSurface)0;
    float3 barycentrics = float3(1 - attributeData.barycentrics.x - attributeData.barycentrics.y,
        attributeData.barycentrics.x, attributeData.barycentrics.y);

    uint3 triangleIndices = UnityRayTracingFetchTriangleIndices(primIndex);
    float2 uv0 = UnityRayTracingFetchVertexAttribute2(triangleIndices.x, kVertexAttributeTexCoord0);
    float2 uv1 = UnityRayTracingFetchVertexAttribute2(triangleIndices.y, kVertexAttributeTexCoord0);
    float2 uv2 = UnityRayTracingFetchVertexAttribute2(triangleIndices.z, kVertexAttributeTexCoord0);
    
    float3 pos0 = UnityRayTracingFetchVertexAttribute3(triangleIndices.x, kVertexAttributePosition);
    float3 pos1 = UnityRayTracingFetchVertexAttribute3(triangleIndices.y, kVertexAttributePosition);
    float3 pos2 = UnityRayTracingFetchVertexAttribute3(triangleIndices.z, kVertexAttributePosition);

    float3 normal0 = UnityRayTracingFetchVertexAttribute3(triangleIndices.x, kVertexAttributeNormal);
    float3 normal1 = UnityRayTracingFetchVertexAttribute3(triangleIndices.y, kVertexAttributeNormal);
    float3 normal2 = UnityRayTracingFetchVertexAttribute3(triangleIndices.z, kVertexAttributeNormal);

    surface.uv = uv0 * barycentrics.x + uv1 * barycentrics.y + uv2 * barycentrics.z;
    surface.position = pos0 * barycentrics.x + pos1 * barycentrics.y + pos2 * barycentrics.z;
    surface.wo = wo;
    surface.normal = normal0 * barycentrics.x + normal1 * barycentrics.y + normal2 * barycentrics.z;

    return surface;
}

float3 MIS_BSDF(HitSurface hitSurface, Material material, inout RNG rng, out PathVertex pathVertex)
{
    float3 ld = float3(0, 0, 0);
    BSDFSample bsdfSample = SampleMaterialBRDF(material, hitSurface, rng);
    float scatteringPdf = bsdfSample.pdf;
    float3 wi = hitSurface.LocalToWorld(bsdfSample.wi);
    float3 f = bsdfSample.reflectance * abs(dot(wi, hitSurface.normal));

    if (!IsBlack(f) && scatteringPdf > 0)
    {
        float3 li = 0;
        float lightPdf = 0;
        RayDesc ray = SpawnRay(hitSurface.position, wi, hitSurface.normal, FLT_MAX);

        RayIntersection rayIntersection;

        //rayIntersection.remainingDepth = 1;
        rayIntersection.color = float4(1.0f, 0.0f, 0.0f, 1.0f);
        rayIntersection.rng = rng;
        rayIntersection.bounce = 0;
        rayIntersection.direction = ray.Direction;

        TraceRay(_AccelerationStructure, RAY_FLAG_CULL_BACK_FACING_TRIANGLES, 0xFF, 0, 1, 0, ray, rayIntersection);

        if (rayIntersection.hitResult == HIT_LIGHT)
        {
            AreaLight hitLight = _Lights[rayIntersection.instanceID];
            float lightSourcePmf = LightSourcePmf(rayIntersection.instanceID);
            lightPdf = AreaLightPdf(hitLight) * lightSourcePmf;
            if (lightPdf > 0)
            {
                li = hitLight.radiance;
            }
        }
        float weight = bsdfSample.IsSpecular() ? 1 : PowerHeuristic(1, scatteringPdf, 1, lightPdf);
        if (!bsdfSample.IsSpecular())
            weight = PowerHeuristic(1, scatteringPdf, 1, lightPdf);
        ld = f * li * weight / scatteringPdf;
    }

    pathVertex.wi = wi;
    pathVertex.bsdfVal = f;
    pathVertex.bsdfPdf = scatteringPdf;

    return ld;
}

float3 MIS_ShadowRay(AreaLight light, HitSurface surface, Material material, float lightSourcePdf, inout RNG rng)
{
    float3 wi;
    float lightPdf = 0;
    float3 samplePointOnLight;
    float3 ld = float3(0, 0, 0);
    float3 Li = SampleLightRadiance(light, surface.position, rng, wi, lightPdf, samplePointOnLight);
    lightPdf *= lightSourcePdf;

    //if (!IsBlack(Li))
    {
        //ShadowRay shadowRay = (ShadowRay)0;

        float3 wiLocal = surface.WorldToLocal(wi);
        float3 woLocal = surface.WorldToLocal(surface.wo.xyz);
        float scatteringPdf = 0;

        float3 f = MaterialBRDF(material, surface, woLocal, wiLocal, scatteringPdf);
        if (!IsBlack(f) && scatteringPdf > 0)
        {
            RayDesc ray = SpawnRay(surface.position, wi, surface.normal, distance(samplePointOnLight, surface.position) - ShadowEpsilon);
            RayIntersection rayIntersection;

            //rayIntersection.remainingDepth = 1;
            rayIntersection.color = float4(1.0f, 0.0f, 0.0f, 1.0f);
            rayIntersection.rng = rng;
            rayIntersection.bounce = 0;
            rayIntersection.direction = ray.Direction;

            TraceRay(_AccelerationStructure, RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER, 0xFF, 0, 1, 0, ray, rayIntersection);

            bool shadowRayVisible = rayIntersection.hitResult > HIT_MISS;
            if (shadowRayVisible)
            {
                f *= abs(dot(wi, surface.normal));
                //sample psdf and compute the mis weight
                float weight =
                    PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                ld = f * Li * weight / lightPdf;
            }
        }
    }

    return ld;
}

float3 EstimateDirectLighting(HitSurface hitSurface, Material material, inout RNG rng, out PathVertex pathVertex, out bool breakPath)
{
    breakPath = false;
    float lightSourcePdf = 1.0;
    int lightIndex = 0;//UniformSampleLightSource(Get1D(rng), _LightsNum, lightSourcePdf);//SampleLightSource(rng, lightSourcePdf, lightIndex);
    AreaLight light = _Lights[lightIndex];
    pathVertex = (PathVertex)0;
    float3 ld = MIS_ShadowRay(light, hitSurface, material, lightSourcePdf, rng);
    ld += MIS_BSDF(hitSurface, material, rng, pathVertex);

    if (pathVertex.bsdfPdf == 0 || MaxValue(pathVertex.bsdfVal) == 0 || MaxValue(ld) == 0)
    {
        breakPath = true;
    }

    return ld;
}

/*
float3 PathLi(HitSurface hitSurface, int bounce, Material material, inout RNG rng)
{
    PathVertex pathVertex = (PathVertex)0;
    BSDFSample bsdfSample = SampleMaterialBRDF(material, hitSurface, rng);
    float scatteringPdf = bsdfSample.pdf;
    float3 wi = hitSurface.LocalToWorld(bsdfSample.wi);

    float3 f = bsdfSample.reflectance * abs(dot(wi, hitSurface.normal));

    int lightIndex = 0;
    float lightSourcePdf = 0;
    AreaLight light = SampleLightSource(rng, lightSourcePdf, lightIndex);
    float lightPdf = 0;
    float3 samplePointOnLight;
    float3 Li = SampleLightRadiance(light, hitSurface.position, rng, wi, lightPdf, samplePointOnLight);
    lightPdf *= lightSourcePdf;
    if (!IsBlack(Li) && !bsdfSample.IsSpecular())
    {
        float3 wiLocal = hitSurface.WorldToLocal(wi);
        float3 woLocal = hitSurface.WorldToLocal(hitSurface.wo.xyz);
        float scatteringPdf = 0;

        float3 f = MaterialBRDF(material, hitSurface, woLocal, wiLocal, scatteringPdf);
        if (!IsBlack(f) && scatteringPdf > 0)
        {
            RayDesc shadowRay = SpawnRay(hitSurface.position, wi, hitSurface.normal, FLT_MAX);

            RayIntersection rayIntersection;

            //rayIntersection.remainingDepth = 1;
            rayIntersection.color = float4(1.0f, 0.0f, 0.0f, 1.0f);
            rayIntersection.rng = rng;
            rayIntersection.bounce = 0;
            rayIntersection.direction = shadowRay.Direction;

            TraceRay(_AccelerationStructure, RAY_FLAG_CULL_BACK_FACING_TRIANGLES | RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH, 0xFF, 0, 1, 0, shadowRay, rayIntersection);

            if (rayIntersection.hitted)
            {
                f *= abs(dot(wi, hitSurface.normal));
                //sample psdf and compute the mis weight
                float weight =
                    PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                ld = f * Li * weight / lightPdf;
            }
        }
    }
    return f;
}
*/
#endif
