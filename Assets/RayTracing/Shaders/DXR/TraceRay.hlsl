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
    int    found;
    HitSurface nextHit;
    Material nextMaterial;
};

struct RayCone
{
    float spreadAngle;
    float width;
};

RayCone Propagate(RayCone preCone, float surfaceSpreadAngle, float hitT)
{
    RayCone newCone;
    newCone.width = preCone.spreadAngle * hitT + preCone.width;
    newCone.spreadAngle = preCone.spreadAngle + surfaceSpreadAngle;
    return newCone;
}

RayCone ComputeRayCone(RayCone preCone, float distance, float pixelSpreadAngle)
{
    //RayCone rayCone;
    //rayCone.width = preSpreadAngle * distance;
    //rayCone.spreadAngle = lastSpreadAngle;
    //float gamma = cameraConeSpreadAngle;
    return Propagate(preCone, pixelSpreadAngle, distance);
}

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
    origin = _CameraPosWS.xyz;
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
    float4 origWorld = mul(_CameraToWorld, float4(orig, 1));
    ray.Origin = origWorld.xyz / origWorld.w;
    float4 directionWorld = mul(_CameraToWorld, float4(direction, 0));
    ray.Direction = normalize(directionWorld.xyz);
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

HitSurface GetHitSurface(uint primIndex, float3 wo, AttributeData attributeData, float3x4 localToWorld, float3x4 worldToLocal)
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

    float2 uv = uv0 * barycentrics.x + uv1 * barycentrics.y + uv2 * barycentrics.z;

    surface.uv = uv;
    //float3 hitPos = float3(pos0 * uv.x + pos1 * uv.y + pos2 * (1.0 - uv.x - uv.y));

    float3 origin = WorldRayOrigin();
    float3 direction = WorldRayDirection();
    surface.hitT = RayTCurrent();
    float3 positionWS = origin + direction * surface.hitT;

    float3 normal = normalize(normal0 * uv.x + normal1 * uv.y + normal2 * (1.0 - uv.x - uv.y));
    float3 worldNormal = normalize(mul(normal, (float3x3)worldToLocal));
    //hitPos = mul(localToWorld, hitPos);
    float3 dpdu = float3(1, 0, 0);
    float3 dpdv = float3(0, 1, 0);
    CoordinateSystem(worldNormal, dpdu, dpdv);
    surface.tangent.xyz = normalize(dpdu.xyz);
    surface.bitangent.xyz = normalize(cross(surface.tangent.xyz, worldNormal));
    surface.position = positionWS;
    surface.wo = wo;
    surface.normal = worldNormal;

    return surface;
}

float3 MIS_BSDF(HitSurface hitSurface, Material material, inout RNG rng, out PathVertex pathVertex)
{
    float3 ld = float3(0, 0, 0);
    pathVertex = (PathVertex)0;
    BSDFSample bsdfSample = SampleMaterialBRDF(material, hitSurface, rng);

    float scatteringPdf = bsdfSample.pdf;
    float3 wi = hitSurface.LocalToWorld(bsdfSample.wi);
    //return normalize(wi);
    float3 f = bsdfSample.reflectance * abs(dot(wi, hitSurface.normal));

    if (!IsBlack(f) && scatteringPdf > 0)
    {
        float3 li = 0;
        float lightPdf = 0;
        RayDesc ray = SpawnRay(hitSurface.position, wi, hitSurface.normal, FLT_MAX);

        PathPayload payLoad;

        payLoad.direction = ray.Direction;
        payLoad.isHitLightCheck = 0;
        payLoad.instanceID = -1;
        payLoad.hitResult = HIT_MISS;

        TraceRay(_AccelerationStructure, /*RAY_FLAG_CULL_BACK_FACING_TRIANGLES*/0, 0xFF, 0, 1, 0, ray, payLoad);

        if (payLoad.hitResult > HIT_MISS)
        {
            pathVertex.found = 1;
            pathVertex.nextHit = payLoad.hitSurface;
            pathVertex.nextMaterial = payLoad.material;
            
            if (payLoad.hitResult == HIT_LIGHT)
            {
                int lightIndex = payLoad.hitSurface.lightIndex;
                AreaLight hitLight = _Lights[lightIndex];
                float lightSourcePmf = LightSourcePmf(lightIndex);
                lightPdf = AreaLightPdf(hitLight) * lightSourcePmf;
                if (lightPdf > 0)
                {
                    li = hitLight.radiance;
                }
            }       
        }
        
        float weight = bsdfSample.IsSpecular() ? 1 : PowerHeuristic(1, scatteringPdf, 1, lightPdf);
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

    if (!IsBlack(Li))
    {
        //ShadowRay shadowRay = (ShadowRay)0;

        float3 wiLocal = surface.WorldToLocal(wi);
        float3 woLocal = surface.WorldToLocal(surface.wo.xyz);
        float scatteringPdf = 0;

        float3 f = MaterialBRDF(material, surface, woLocal, wiLocal, scatteringPdf);
        if (!IsBlack(f) && scatteringPdf > 0)
        {
            RayDesc ray = SpawnRay(surface.position, samplePointOnLight - surface.position, surface.normal, 1.0 - ShadowEpsilon);
            PathPayload payLoad;

            payLoad.direction = ray.Direction;
            payLoad.instanceID = -1;
            payLoad.isHitLightCheck = 0;
            payLoad.hitResult = HIT_MISS;

            TraceRay(_AccelerationStructure, RAY_FLAG_ACCEPT_FIRST_HIT_AND_END_SEARCH | RAY_FLAG_SKIP_CLOSEST_HIT_SHADER, 0xFF, 0, 1, 0, ray, payLoad);

            bool shadowRayVisible = payLoad.hitResult == HIT_MISS;
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

float3 EstimateDirectLighting(HitSurface hitSurface, Material material, inout RNG rng, out PathVertex pathVertex, bool breakPath)
{
    breakPath = false;
    float lightSourcePdf = 1.0;
    int lightIndex = UniformSampleLightSource(Get1D(rng), _LightsNum, lightSourcePdf);//SampleLightSource(rng, lightSourcePdf, lightIndex);
    AreaLight light = _Lights[lightIndex];
    pathVertex = (PathVertex)0;
    float3 ld = MIS_ShadowRay(light, hitSurface, material, lightSourcePdf, rng);
    ld += MIS_BSDF(hitSurface, material, rng, pathVertex);

    if (pathVertex.bsdfPdf == 0 || MaxValue(pathVertex.bsdfVal) == 0)
    {
        breakPath = true;
    }

    return ld;
}

float3 PathLi(RayDesc ray, uint2 id, inout RNG rng)
{
    float3 li = 0;
    float3  beta = 1;
    PathVertex pathVertex = (PathVertex)0;
    PathPayload payLoad;
    payLoad.direction = ray.Direction.xyz;
    payLoad.instanceID = -1;
    payLoad.isHitLightCheck = 0;
    payLoad.hitResult = 0;
    HitSurface hitCur;
    Material material;

    for (int bounces = 0; bounces < _MaxDepth; bounces++)
    {
        bool foundIntersect = false;
        if (bounces == 0)
        {
            TraceRay(_AccelerationStructure, /*RAY_FLAG_CULL_BACK_FACING_TRIANGLES*/0, 0xFF, 0, 1, 0, ray, payLoad);
            foundIntersect = payLoad.hitResult > 0;
            if (foundIntersect)
            {
                hitCur = payLoad.hitSurface;
                material = payLoad.material;
            }
        }
        else
        {
            foundIntersect = pathVertex.found == 1;
        }
        
        if (foundIntersect)
        {
            int lightIndex = hitCur.lightIndex;

            half4 surfaceBeta = _RayConeGBuffer[id.xy];
            RayCone preCone;
            preCone.width = surfaceBeta.z;
            preCone.spreadAngle = surfaceBeta.y;
            if (bounces == 0)
            {
                surfaceBeta.y = _CameraConeSpreadAngle + surfaceBeta.x;
                hitCur.coneWidth = _CameraConeSpreadAngle * hitCur.hitT;
            }
            else
            {
                RayCone rayCone = ComputeRayCone(preCone, hitCur.hitT, surfaceBeta.r);
                hitCur.coneWidth = rayCone.width;
            }

            if (lightIndex >= 0 && bounces == 0)
            {
                AreaLight light = _Lights[lightIndex];
                li += light.radiance * beta;
                break;
            }

            bool breakPath = false;
            float3 ld = EstimateDirectLighting(hitCur, material, rng, pathVertex, breakPath);
            li += ld * beta;

            if (breakPath)
            {
                //return pathVertex.wi;
                break;
            }

            float3 throughput = pathVertex.bsdfVal / pathVertex.bsdfPdf;
            beta *= throughput;

            //Russian roulette
            if (bounces > _MinDepth)
            {
                float q = max(0.05, 1 - MaxComponent(beta));
                if (Get1D(rng) < q)
                {
                    break;
                }
                else
                    beta /= 1 - q;
            }
        }
        else
        {
            //sample enviroment map
            //if (bounces == 0 && _EnvLightIndex >= 0)
            //{
            //    li += beta * EnviromentLightLe(ray.direction);
            //}
            
            break;
        }
        hitCur = pathVertex.nextHit;
        material = pathVertex.nextMaterial;
    }
    
    return li;
}
#endif
