using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RayTracingTest
{
    static float INV_PI      = 0.31830988618379067154f;
    static float INV_TWO_PI  = 0.15915494309189533577f;
    static float INV_FOUR_PI = 0.07957747154594766788f;
    static float HALF_PI     = 1.57079632679489661923f;
    static float INV_HALF_PI = 0.63661977236758134308f;
    static float PI_OVER_2 = 1.57079632679489661923f;
    static float PI_OVER_4 = 0.78539816339744830961f;
    //同心圆盘采样
    static Vector2 ConcentricSampleDisk(Vector2 u)
    {
        //mapping u to [-1,1]
        Vector2 u1 = new Vector2(u.x * 2.0f - 1, u.y * 2.0f - 1);

        if (u1.x == 0 && u1.y == 0)
            return Vector2.zero;

        //r = x
        //θ = y/x * π/4
        //最后返回x,y
        //x = rcosθ, y = rsinθ
        float theta, r;
        if (Mathf.Abs(u1.x) > Mathf.Abs(u1.y))
        {
            r = u1.x;
            theta = u1.y / u1.x * PI_OVER_4;
        }
        else
        {
            //这里要反过来看，就是把视野选择90度
            r = u1.y;
            theta = PI_OVER_2 - u1.x / u1.y * PI_OVER_4;
        }
        return r * new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
    }

    static Vector3 CosineSampleHemisphere(Vector2 u)
    {
        Vector2 rphi = ConcentricSampleDisk(u);

        float z = Mathf.Sqrt(1.0f - rphi.x * rphi.x - rphi.y * rphi.y);
        return new Vector3(rphi.x, rphi.y, z);
    }
    static bool SameHemisphere(Vector3 w, Vector3 wp)
    {
        return w.z * wp.z > 0;
    }

    static Vector3 LambertBRDF(Vector3 wi, Vector3 wo, Vector3 R)
    {
        return R * (1.0f / Mathf.PI);
    }

    static float AbsCosTheta(Vector3 w)
    {
        return Mathf.Abs(w.z);
    }

    //wi and wo must in local space
    static float LambertPDF(Vector3 wi, Vector3 wo)
    {
        return SameHemisphere(wo, wi) ? AbsCosTheta(wi) * (1.0f / Mathf.PI) : 0;
    }

    static Vector3 SampleLambert(GPUMaterial material, Vector3 wo, out Vector3 wi, Vector2 u, out float pdf)
    {
        wi = CosineSampleHemisphere(u);
        if (wo.z < 0)
            wi.z *= -1;
        pdf = LambertPDF(wi, wo);
        return LambertBRDF(wi, wo, material.baseColor);
    }
    static float PowerHeuristic(int nf, float fPdf, int ng, float gPdf)
    {
        float f = nf * fPdf, g = ng * gPdf;
        return (f * f) / (f * f + g * g);
    }

    
    static int FindIntervalSmall(int start, int size, float u, List<Vector2> func)
    {
        int first = 0, len = size;
        while (len > 0)
        {
            int nHalf = len >> 1;
            int middle = first + nHalf;
            // Bisect range based on value of _pred_ at _middle_
            Vector2 distrubution = func[start + middle];
            if (distrubution.y <= u)
            {
                first = middle + 1;
                len -= nHalf + 1;
            }
            else
                len = nHalf;
        }
        return Mathf.Clamp(first - 1, 0, size - 2) + start;
    }
    /*
    static int SampleDistribution1DDiscrete(List<Vector2> Distributions1D, float u, int start, int num, out float pdf)
    {
        int offset = FindIntervalSmall(Distributions1D, start, num, u);
        pdf = Distributions1D[start + offset].x;
        return offset;
    }
    */
    public static Vector3 FrConductor(float cosThetaI, Vector3 etai, Vector3 etat, Vector3 k)
    {
        cosThetaI = Mathf.Clamp(cosThetaI, -1, 1);
        Vector3 eta = etat.Div(etai);
        Vector3 etak = k.Div(etai);

        float cosThetaI2 = cosThetaI * cosThetaI;
        float sinThetaI2 = 1.0f - cosThetaI2;
        Vector3 eta2 = eta.Mul(eta);
        Vector3 etak2 = etak.Mul(etak);

        Vector3 t0 = eta2 - etak2 - new Vector3(sinThetaI2, sinThetaI2, sinThetaI2);
        Vector3 a2plusb2 = (t0.Mul(t0) + eta2.Mul(etak2) * 4).Sqrt();
        Vector3 t1 = a2plusb2.Add(cosThetaI2);
        Vector3 a = ((a2plusb2 + t0) * 0.5f).Sqrt();
        Vector3 t2 = 2.0f * cosThetaI * a;
        Vector3 Rs = (t1 - t2).Div(t1 + t2);

        Vector3 t3 = (a2plusb2 * cosThetaI2).Add(sinThetaI2 * sinThetaI2);
        Vector3 t4 = t2 * sinThetaI2;
        Vector3 Rp = Rs.Mul( (t3 - t4).Div(t3 + t4) );

        return 0.5f * (Rp + Rs);
    }

    public static void SampleLightTest(List<Vector2> Distributions1D, List<GPULight> gpuLights, List<MeshInstance> meshInstances, 
        List<int> triangles, List<GPUVertex> gpuVertices, List<GPUDistributionDiscript> gpuDistributionDiscripts)
    {
        GPULight SampleLightSource(float u, out float pdf, out int index)
        {
            index = GPUDistributionTest.Sample1DDiscrete(u, gpuDistributionDiscripts[0], Distributions1D, out pdf); //SampleDistribution1DDiscrete(Distributions1D, u, 0, gpuLights.Count, out pdf);
            return gpuLights[index];
        }

        Vector3 SampleTrianglePoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 u, out Vector3 normal, out float pdf)
        {
            //caculate bery centric uv w = 1 - u - v
            float t = Mathf.Sqrt(u.x);
            Vector2 uv = new Vector2(1.0f - t, t * u.y);
            float w = 1 - uv.x - uv.y;

            Vector3 position = p0 * w + p1 * uv.x + p2 * uv.y;
            Vector3 crossVector = Vector3.Cross(p1 - p0, p2 - p0);
            normal = crossVector.normalized;
            pdf = 1.0f / crossVector.magnitude;

            return position;
        }

        Vector3 SampleTriangleLightRadiance(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 u, Vector3 p, Vector3 normal, GPULight light, out Vector3 wi, out Vector3 position, out float pdf)
        {
            Vector3 Li = light.radiance;
            Vector3 lightPointNormal;
            float triPdf = 0;
            position = SampleTrianglePoint(p0, p1, p2, u, out lightPointNormal, out triPdf);
            pdf = triPdf;
            wi = position - p;
            float wiLength = Vector3.Magnitude(wi);
            if (wiLength == 0)
            {
                Li = Vector3.zero;
                pdf = 0;
            }
            wi = Vector3.Normalize(wi);
            pdf *= wiLength * wiLength / Mathf.Abs(Vector3.Dot(lightPointNormal, -wi));

            return Li;
        }

        float u = UnityEngine.Random.Range(0.0f, 1.0f);

        int lightIndex = 0;
        float lightSourcePdf = 0;
        GPULight gpuLight = SampleLightSource(u, out lightSourcePdf, out lightIndex);

        u = UnityEngine.Random.Range(0.0f, 1.0f);
        MeshInstance meshInstance = meshInstances[gpuLight.meshInstanceID];
        float lightPdf = 0;
        int triangleIndex = GPUDistributionTest.Sample1DDiscrete(u, gpuDistributionDiscripts[gpuLight.distributionDiscriptIndex], 
            Distributions1D, out lightPdf) * 3 + meshInstance.triangleStartOffset;
        //(SampleLightTriangle(gpuLight.distributeAddress, gpuLight.trianglesNum, u, out lightPdf) - gpuLights.Count) * 3 + meshInstance.triangleStartOffset;

        int vertexStart = triangleIndex;
        int vIndex0 = triangles[vertexStart];
        int vIndex1 = triangles[vertexStart + 1];
        int vIndex2 = triangles[vertexStart + 2];
        Vector3 p0 = gpuVertices[vIndex0].position;
        Vector3 p1 = gpuVertices[vIndex1].position;
        Vector3 p2 = gpuVertices[vIndex2].position;
        //convert to worldpos

        p0 = meshInstance.localToWorld.MultiplyPoint(p0);
        p1 = meshInstance.localToWorld.MultiplyPoint(p1);
        p2 = meshInstance.localToWorld.MultiplyPoint(p2);

        //Vector3 lightPointNormal;
        Vector3 trianglePoint;
        //SampleTrianglePoint(p0, p1, p2, rs.Get2D(threadId), lightPointNormal, trianglePoint, triPdf);
        Vector3 wi;
        Vector2 uv = new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
        float triPdf = 0.0f;
        Vector3 Li = SampleTriangleLightRadiance(p0, p1, p2, uv, new Vector3(-1.8f, 2.7f, 2.2f), Vector3.up, gpuLight, out wi, out trianglePoint, out triPdf);
        lightPdf *= triPdf;
    }

    public static int SampleLightSource(float u, int lightCount, List<Vector2> Distributions1D, out float pmf)
    {
        GPUDistributionDiscript discript = new GPUDistributionDiscript();
        discript.start = 0;
        discript.num = lightCount;
        int index = GPUDistributionTest.Sample1DDiscrete(u, discript, Distributions1D, out pmf); //SampleDistribution1DDiscrete(rs.Get1D(threadId), 0, lightCount, pdf);
        return index;
    }

    static int SampleTriangleIndexOfLightPoint(float u, GPUDistributionDiscript discript, List<Vector2> Distributions1D, out float pdf)
    {
        //get light mesh triangle index
        int index = GPUDistributionTest.Sample1DDiscrete(u, discript, Distributions1D, out pdf);//SampleDistribution1DDiscrete(Distributions1D, u, start, count, out pdf);
        return index;
    }

    static Vector3 SampleTrianglePoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 u, out Vector3 normal, out float pdf)
    {
        //caculate bery centric uv w = 1 - u - v
        float t = Mathf.Sqrt(u.x);
        Vector2 uv = new Vector2(1.0f - t, t * u.y);
        float w = 1 - uv.x - uv.y;

        Vector3 position = p0 * w + p1 * uv.x + p2 * uv.y;
        Vector3 crossVector = Vector3.Cross(p1 - p0, p2 - p0);
        normal = crossVector.normalized;
        pdf = 1.0f / crossVector.magnitude;

        return position;
    }

    static Vector3 SampleTriangleLight(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 u, GPUInteraction isect, GPULight light, out Vector3 wi, out Vector3 position, out float pdf)
    {
        Vector3 Li = light.radiance;
        Vector3 lightPointNormal;
        float triPdf = 0;
        position = SampleTrianglePoint(p0, p1, p2, u, out lightPointNormal, out triPdf);
        pdf = triPdf;
        wi = position - (Vector3)isect.p;
        float wiLength = Vector3.Magnitude(wi);
        if (wiLength == 0 || pdf == 0)
        {
            pdf = 0;
            return Vector3.zero;
        }
        wi = Vector3.Normalize(wi);
        float cos = Vector3.Dot(lightPointNormal, -wi);
        pdf *= wiLength * wiLength / Mathf.Abs(cos);
        if (float.IsInfinity(pdf))
            pdf = 0.0f;

        return cos <= 0 ? Vector3.zero : Li;
    }

    public static Vector3 SampleLightRadiance(List<Vector2> Distributions1D, GPULight light, GPUInteraction isect, 
        List<MeshInstance> meshInstances, List<int> TriangleIndices, List<GPUVertex> Vertices, List<GPUDistributionDiscript> DistributionDiscripts, 
        out Vector3 wi, out float lightPdf, out Vector3 lightPoint)
    {
        if (light.type == 0)
        {
            int discriptIndex = light.distributionDiscriptIndex;
            GPUDistributionDiscript lightDistributionDiscript = DistributionDiscripts[discriptIndex];
            float u = UnityEngine.Random.Range(0.0f, 1.0f);
            float triPdf = 0;
            lightPdf = 0;
            MeshInstance meshInstance = meshInstances[light.meshInstanceID];
            int triangleIndex = SampleTriangleIndexOfLightPoint(u, lightDistributionDiscript, Distributions1D, out lightPdf) * 3 + meshInstance.triangleStartOffset;

            int vertexStart = triangleIndex;
            int vIndex0 = TriangleIndices[vertexStart];
            int vIndex1 = TriangleIndices[vertexStart + 1];
            int vIndex2 = TriangleIndices[vertexStart + 2];
            Vector3 p0 = Vertices[vIndex0].position;
            Vector3 p1 = Vertices[vIndex1].position;
            Vector3 p2 = Vertices[vIndex2].position;
            //convert to worldpos

            p0 = meshInstance.localToWorld.MultiplyPoint(p0); //mul(meshInstance.localToWorld, float4(p0, 1)).xyz;
            p1 = meshInstance.localToWorld.MultiplyPoint(p1); //mul(meshInstance.localToWorld, float4(p1, 1)).xyz;
            p2 = meshInstance.localToWorld.MultiplyPoint(p2); //mul(meshInstance.localToWorld, float4(p2, 1)).xyz;

            Vector3 Li = SampleTriangleLight(p0, p1, p2, new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f)), isect, light, out wi, out lightPoint, out triPdf);
            lightPdf *= triPdf;
            return Li;
        }
        else if (light.type == 1)
        {
            Vector2 u = new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
            float mapPdf = 1.0f / (4.0f * Mathf.PI);
            float theta = u[1] * Mathf.PI;
            float phi = u[0] * 2 * Mathf.PI;
            float cosTheta = Mathf.Cos(theta);
            float sinTheta = Mathf.Sin(theta);
            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);
            wi = isect.LocalToWorld(new Vector3(sinTheta * cosPhi, sinTheta * sinPhi, cosTheta));
            lightPdf = mapPdf;
            lightPoint = isect.p + wi * 10000.0f;
            return Vector3.one;
        }
        lightPdf = 0;
        lightPoint = Vector3.zero;
        wi = Vector3.zero;
        return Vector3.zero;
    }

    public static GPUShadowRay SampleShadowRay(GPULight light, GPUInteraction isect, BVHAccel bvhAccel, int instBVHOffset, List<Vector2> Distributions1D,
        List<MeshInstance> meshInstances, List<int> TriangleIndices, List<GPUVertex> Vertices, List<GPUMaterial> materials, List<GPUDistributionDiscript> gpuDistributionDiscripts)
    {
        GPUShadowRay shadowRay = new GPUShadowRay();
        Vector3 wi;
        float lightPdf = 0;
        Vector3 samplePointOnLight;
        Vector3 Li = SampleLightRadiance(Distributions1D, light, isect, meshInstances, TriangleIndices, Vertices, gpuDistributionDiscripts, out wi, out lightPdf, out samplePointOnLight);

        if (lightPdf > 0)
        {
            Vector3 p0 = isect.p;
            Vector3 p1 = samplePointOnLight;
            //shadowRay.pdf = triPdf;
            //shadowRay.lightPdf = lightPdf;
            //Vector3 Li = light.radiance;
            //shadowRay.lightNormal = lightPointNormal;
            //Vector3 wi = normalize(shadowRay.p1 - shadowRay.p0);

            //sample bsdf
            GPUMaterial material = materials[(int)isect.materialID];
            Vector3 wiLocal = isect.WorldToLocal(wi);
            Vector3 woLocal = isect.WorldToLocal(isect.wo);
            float cos = Vector3.Dot(wi, isect.normal);

            Vector3 f = LambertBRDF(woLocal, wiLocal, new Vector3(material.baseColor.x, material.baseColor.y, material.baseColor.z)) * Mathf.Abs(Vector3.Dot(wi, isect.normal));
            float scatteringPdf = LambertPDF(wiLocal, woLocal);
            int meshInstanceIndex = -1;
            float hitT = 0;
            if (ShadowRayVisibilityTest(p0, p1, isect.normal, bvhAccel, meshInstances, instBVHOffset, out hitT, out meshInstanceIndex))
            {
                //sample psdf and compute the mis weight
                float weight =
                    PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                shadowRay.radiance = new Vector3(f.x * Li.x, f.y * Li.y, f.z * Li.z) * weight / lightPdf;
                //shadowRay.visibility = 1;
            }
            else
            {
                shadowRay.radiance = Vector3.zero;
                //shadowRay.visibility = 0;
            }
        }


        return shadowRay;
    }

    public static GPUShadowRay SampleShadowRayTest(BVHAccel bvhAccel, int instBVHOffset, List<Vector2> Distributions1D, List<GPULight> gpuLights, GPUInteraction isect, 
        List<MeshInstance> meshInstances, List<int> TriangleIndices, List<GPUVertex> Vertices, List<GPUMaterial> materials, List<GPUDistributionDiscript> gpuDistributionDiscripts)
    {
        

		//int distributionAddress = light.distributeAddress;
		float u = UnityEngine.Random.Range(0.0f, 1.0f);
        float lightSourcePdf = 0;
        int lightIndex = SampleLightSource(u, gpuLights.Count, Distributions1D, out lightSourcePdf);
        GPULight light = gpuLights[lightIndex];

        return SampleShadowRay(light, isect, bvhAccel, instBVHOffset, Distributions1D, meshInstances, TriangleIndices, Vertices, materials, gpuDistributionDiscripts);

	}

    static float AreaLightPdf(BVHAccel bvhaccel, GPURay ray, GPULight light, int lightsNum, 
        List<MeshInstance> meshInstances, List<int> TriangleIndices, List<GPUVertex> Vertices, List<Vector2> distributions1D)
    {
        float lightPdf = 0;
        //intersect the light mesh triangle
        if (light.type == 0)
        {
            /*
            float bvhHit = ray.tmax;
            int meshHitTriangleIndex;  //wood triangle addr
            
            int distributionIndex = lightsNum;
            //getting the mesh of the light
            MeshInstance meshInstance = meshInstances[light.meshInstanceID];

            //convert to mesh local space
            GPURay rayTemp = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);
            */
            return 1.0f / light.area;
        }
        else
        {
            lightPdf = 1.0f / (4.0f * Mathf.PI);
        }

        return lightPdf;
    }


    public static GPURay GeneratePath(ref GPUInteraction isect, ref GPUPathRadiance pathRadiance, GPURay ray, List<GPUMaterial> materials, out bool breakLoop, out float pdf)
    {
        breakLoop = false;
        Vector3 beta = pathRadiance.beta;
        GPUMaterial material = materials[(int)isect.materialID];
        Vector3 woLocal = isect.WorldToLocal(/*isect.wo*/-ray.direction);
        Vector3 wi;
        float scatteringPdf = 0;
        Vector3 wiLocal;
        Vector2 u = new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
        Vector3 f = SampleLambert(material, woLocal, out wiLocal, u, out scatteringPdf);
        if (f == Vector3.zero || scatteringPdf == 0)
        {
            //terminate the path tracing
            breakLoop = true;
        }
        pdf = scatteringPdf;
        wi = isect.LocalToWorld(wiLocal);
        beta = beta.Mul(f * Mathf.Abs(Vector3.Dot(wi, isect.normal)) / scatteringPdf);
        pathRadiance.beta = beta;

        GPURay rayNew = SpawnRay(isect.p, wi, isect.normal, float.MaxValue);

        return rayNew;
    }

    public static GPURay SpawnRay(Vector3 p, Vector3 direction, Vector3 normal, float tMax)
    {
        GPURay ray = new GPURay();
        ray.orig = BVHAccel.offset_ray(p, normal);
        ray.tmax = tMax;
        ray.direction = direction;
        ray.tmin = 0;
        return ray;
    }

    public static bool ShadowRayVisibilityTest(Vector3 p0, Vector3 p1, Vector3 normal, BVHAccel bvhAccel, List<MeshInstance> meshInstances, int instBVHOffset, out float hitT, out int meshInstanceIndex)
	{
		GPURay ray = new GPURay();
		ray.orig = BVHAccel.offset_ray(p0, normal);
		ray.tmax = 1.0f - 0.0001f;
		ray.direction = p1 - p0;
		ray.tmin = 0;
        hitT = 0;

        return !bvhAccel.IntersectInstTestP(ray, meshInstances, instBVHOffset, out hitT, out meshInstanceIndex);
	}

    public static GPURay GenerateRay(int x, int y, Matrix4x4 RasterToCamera, Matrix4x4 CameraToWorld, Filter filter)
    {
        Vector2 u = MathUtil.GetRandom01();
        GPUDistributionDiscript discript = new GPUDistributionDiscript();
        discript.start = 0;
        Vector2Int size = filter.GetDistributionSize();
        discript.num = size.y;
        discript.unum = size.x;
        Bounds2D domain = filter.GetDomain();
        discript.domain = new Vector4(domain.min[0], domain.max[0], domain.min[1], domain.max[1]);
        float pdf = 0;
        Vector2 sample = GPUDistributionTest.Sample2DContinuous(u, discript, 
            filter.SampleDistributions(), out pdf);

        sample += new Vector2(x, y) + new Vector2(0.5f, 0.5f);

        Vector3 pFilm = new Vector3(sample.x, sample.y, 0);
        Vector3 nearplanePoint = RasterToCamera.MultiplyPoint(pFilm);
        nearplanePoint.Normalize();
        //nearplanePoint /= nearplanePoint.w;

        GPURay ray = new GPURay();
        ray.orig = CameraToWorld.MultiplyPoint(Vector3.zero);
        ray.direction = CameraToWorld.MultiplyVector(nearplanePoint);
        ray.tmax = float.MaxValue;
        ray.tmin = 0;
        return ray;
    }

    public static float SampleDistribution1DContinous(float u, GPUDistributionDiscript discript, Vector2 domain, List<Vector2> funcs, out float pdf, out int off)
    {
        // Find surrounding CDF segments and _offset_
        int cdfSize = discript.num + 1;
        int offset = FindIntervalSmall(discript.start, cdfSize, u, funcs);
        off = offset;
        // Compute offset along CDF segment
        float du = u - funcs[offset].y;
        if ((funcs[offset + 1].y - funcs[offset].y) > 0)
        {
            du /= (funcs[offset + 1].y - funcs[offset].y);
        }

        // Compute PDF for sampled offset
        pdf = funcs[offset].x / discript.funcInt;//(distribution.funcInt > 0) ? funcs[offset].x / distribution.funcInt : 0;

        // Return $x\in{}[0,1)$ corresponding to sample
        return Mathf.Lerp(domain.x, domain.y, (offset - discript.start + du) / discript.num);
    }

    public static Vector2 SampleDistribution2DContinous(Vector2 u, GPUDistributionDiscript discript, List<Vector2> marginal, List<Vector2> conditions, List<float> conditaionalFuncInts, out float pdf)
    {
        float pdfMarginal;
        int v;
        float d1 = SampleDistribution1DContinous(u.y, discript, new Vector2(discript.domain.x, discript.domain.y), marginal, out pdfMarginal, out v);
        int nu;
        float pdfCondition;
        GPUDistributionDiscript dCondition = new GPUDistributionDiscript();
        dCondition.start = v * (discript.unum + 1);
        dCondition.num = discript.unum;
        dCondition.funcInt = conditaionalFuncInts[v];
        float d0 = SampleDistribution1DContinous(u.x, dCondition, new Vector2(discript.domain.z, discript.domain.w), conditions, out pdfCondition, out nu);
        //p(v|u) = p(u,v) / pv(u)
        //so 
        //p(u,v) = p(v|u) * pv(u)
        pdf = pdfCondition * pdfMarginal;
        return new Vector2(d0, d1);
    }

    public static Vector2 ImportanceSampleEnvmap(Vector2 u, GPUDistributionDiscript discript, List<Vector2> marginal,
        List<Vector2> conditions, List<float> conditionalFuncInts, out float pdf)
    {
        float mapPdf = 0;
        pdf = 0;
        Vector2 uv = SampleDistribution2DContinous(u, discript, marginal, conditions, conditionalFuncInts, out mapPdf);
        if (mapPdf == 0)
            return Vector2.zero;
        // Convert infinite light sample point to direction
        float theta = uv.y * Mathf.PI;
        float phi = uv.x * 2 * Mathf.PI;
        float cosTheta = Mathf.Cos(theta);
        float sinTheta = Mathf.Sin(theta);
        float sinPhi = Mathf.Sin(phi);
        float cosPhi = Mathf.Cos(phi);
        //wi = float3(sinTheta * cosPhi, sinTheta * sinPhi, cosTheta);

        // Compute PDF for sampled infinite light direction
        pdf = mapPdf / (2 * Mathf.PI * Mathf.PI * sinTheta);
        if (sinTheta == 0)
        {
            pdf = 0;
            return Vector2.zero;
        }

        return uv;
    }

    public static Vector3 OnePathTracing(int x, int y, int rasterWidth, int spp, GPUSceneData scene, Filter filter, Camera camera)
    {
        Debug.Log("###############OnePathtracing debug begin:x=" + x + " y=" + y);
        int index = x + y * rasterWidth;
        //index = 700 + 360 * (int)rasterWidth;
        //GPURay gpuRay = gpuRays[index];
        Vector3 rgbSum = Vector3.zero;
        Vector3 finalRadiance = Vector3.zero;
        for (int i = 0; i < spp; ++i)
        {
            GPURay gpuRay = RayTracingTest.GenerateRay(x, y, scene.RasterToCamera, camera.cameraToWorldMatrix, filter);

            CPUPathIntegrator.PathLi(gpuRay, scene);
        }

        return finalRadiance;
    }

    /*
    private void TestRay(Camera camera, float duration)
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        Ray testRay = camera.ScreenPointToRay(new Vector3(rasterWidth - 1, 0, 0));
        Debug.DrawRay(testRay.origin, testRay.direction * 20.0f, Color.white, duration);
        Ray testRay2 = camera.ScreenPointToRay(new Vector3(0, 530, 0));
        Debug.DrawRay(testRay2.origin, testRay2.direction * 20.0f, Color.blue, duration);


        rayBuffer.GetData(gpuRays);


        int x = 226;//(int)rasterWidth / 2 + 60;
        int y = 280;//(int)rasterHeight / 2;
        int index = x + y * (int)rasterWidth;
        //index = 700 + 360 * (int)rasterWidth;
        GPURay gpuRay = gpuRays[index];

        //bIntersectTest = IntersectRay(bvhAccel.linearNodes[0].bounds, gpuRay);
        float hitT = float.MaxValue;
        GPUInteraction interaction;
        bool bIntersectTest = useInstanceBVH ? bvhAccel.IntersectInstTest(gpuRay, meshInstances, meshHandles, bvhAccel.instBVHNodeAddr, out hitT, out interaction) : bvhAccel.IntersectBVHTriangleTest(gpuRay, 0, out hitT);//SceneIntersectTest(gpuRay);
        if (bIntersectTest)
        {
            Debug.DrawRay(gpuRay.orig, gpuRay.direction * hitT, Color.blue, duration);
        }
        else
        {
            Debug.DrawRay(gpuRay.orig, gpuRay.direction * 20.0f, Color.red, duration);
        }
        //testRay2 = camera.ScreenPointToRay(new Vector3(0, (int)rasterHeight / 2, 0));
        //Debug.DrawRay(testRay2.origin, testRay2.direction * 20.0f, Color.yellow, 100.0f);

        Vector3 nearPlanePoint = RasterToCamera.MultiplyPoint(new Vector3(rasterWidth - 1, 0, 0));
        Vector3 orig = camera.cameraToWorldMatrix.MultiplyPoint(Vector3.zero);
        Vector3 dir = camera.cameraToWorldMatrix.MultiplyPoint(nearPlanePoint) - orig;
        dir.Normalize();
        Ray cpuRay = new Ray();
        cpuRay.origin = orig;
        cpuRay.direction = dir;
        Debug.DrawRay(cpuRay.origin, cpuRay.direction * 20.0f, Color.cyan, duration);
        //test end
    }


    private void TestPath()
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        //rayBuffer.GetData(gpuRays);

        int x = 358;//216;//(int)rasterWidth / 2 + 60;
        int y = 477;//206;//(int)rasterHeight / 2;
        //int x = 361;
        //int y = 420;
        OnePathTracing(x, y, (int)rasterWidth, 1);
    }

    public void IntersectionTest()
    {
        GPURay gpuRay = new GPURay();
        gpuRay.orig = new Vector3(0.02604653f, 4.881493f, -4.587191f);
        gpuRay.direction = new Vector3(-0.4131184f, 0.4631883f, 0.7840853f);
        Debug.DrawRay(gpuRay.orig, gpuRay.direction * 10.0f, Color.green, 100.0f);
        gpuRay.tmax = float.MaxValue;
        gpuRay.tmin = 0;
        float hitT = float.MaxValue;
        GPUInteraction interaction = new GPUInteraction();
        bool bIntersectTest = bvhAccel.IntersectInstTest(gpuRay, meshInstances, meshHandles, bvhAccel.instBVHNodeAddr, out hitT, out interaction);
        if (!bIntersectTest)
        {
            Debug.Log("IntersectionTest failed!");
        }
    }

    //test the light sampling
    
    void SampleLightTest(GPUSceneData gpuScene)
    {
        GPULight SampleLightSource(float u, out float pdf, out int index)
        {
            index = GPUDistributionTest.Sample1DDiscrete(u, gpuScene.gpuDistributionDiscripts[0], gpuScene.Distributions1D, out pdf);
            return gpuScene.gpuLights[index];
        }

        Vector3 SampleTrianglePoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 u, out Vector3 normal, out float pdf)
        {
            //caculate bery centric uv w = 1 - u - v
            float t = Mathf.Sqrt(u.x);
            Vector2 uv = new Vector2(1.0f - t, t * u.y);
            float w = 1 - uv.x - uv.y;

            Vector3 position = p0 * w + p1 * uv.x + p2 * uv.y;
            Vector3 crossVector = Vector3.Cross(p1 - p0, p2 - p0);
            normal = crossVector.normalized;
            pdf = 1.0f / crossVector.magnitude;

            return position;
        }

        Vector3 SampleTriangleLightRadiance(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 u, Vector3 p, Vector3 normal, GPULight light, out Vector3 wi, out Vector3 position, out float pdf)
        {
            Vector3 Li = light.radiance;
            Vector3 lightPointNormal;
            float triPdf = 0;
            position = SampleTrianglePoint(p0, p1, p2, u, out lightPointNormal, out triPdf);
            pdf = triPdf;
            wi = position - p;
            float wiLength = Vector3.Magnitude(wi);
            if (wiLength == 0)
            {
                Li = Vector3.zero;
                pdf = 0;
            }
            wi = Vector3.Normalize(wi);
            pdf *= wiLength * wiLength / Mathf.Abs(Vector3.Dot(lightPointNormal, -wi));

            return Li;
        }

        int lightIndex = 0;
        float lightSourcePdf = 0;
        float u = UnityEngine.Random.Range(0.0f, 1.0f);
        GPULight gpuLight = SampleLightSource(u, out lightSourcePdf, out lightIndex);

        if (gpuLight.type == (int)AreaLightInstance.LightType.Envmap)
        { 
            Vector2 u2 = new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
            GPUDistributionDiscript discript = new GPUDistributionDiscript();
            
            EnviromentLight envLight = gpuScene.areaLightInstances[lightIndex] as EnviromentLight;
            discript.funcInt = envLight.envmapDistributions.Intergal();
            discript.num = envLight.envmapDistributions.size.y;
            discript.unum = envLight.envmapDistributions.size.x;
            discript.domain = new Vector4(0, 1, 0, 1);
            float lightPdf = 0;
            RayTracingTest.ImportanceSampleEnvmap(u2, discript, envLight.envmapDistributions.GetGPUMarginalDistributions(), envLight.envmapDistributions.GetGPUConditionalDistributions(),
                envLight.envmapDistributions.GetGPUConditionFuncInts(), out lightPdf);
        }
        else
        {
            u = UnityEngine.Random.Range(0.0f, 1.0f);
            MeshInstance meshInstance = gpuScene.meshInstances[gpuLight.meshInstanceID];
            float lightPdf = 0;
            int triangleIndex = (GPUDistributionTest.Sample1DDiscrete(u, gpuScene.gpuDistributionDiscripts[gpuLight.distributionDiscriptIndex], 
                gpuScene.Distributions1D, out lightPdf) - gpuScene.gpuLights.Count) * 3 + meshInstance.triangleStartOffset;
            //(SampleLightTriangle(gpuLight.distributeAddress, gpuLight.trianglesNum, u, out lightPdf) - gpuLights.Count) * 3 + meshInstance.triangleStartOffset;

            int vertexStart = triangleIndex;
            int vIndex0 = gpuScene.triangles[vertexStart];
            int vIndex1 = gpuScene.triangles[vertexStart + 1];
            int vIndex2 = gpuScene.triangles[vertexStart + 2];
            Vector3 p0 = gpuScene.gpuVertices[vIndex0].position;
            Vector3 p1 = gpuScene.gpuVertices[vIndex1].position;
            Vector3 p2 = gpuScene.gpuVertices[vIndex2].position;
            //convert to worldpos

            p0 = meshInstance.localToWorld.MultiplyPoint(p0);
            p1 = meshInstance.localToWorld.MultiplyPoint(p1);
            p2 = meshInstance.localToWorld.MultiplyPoint(p2);

            //float3 lightPointNormal;
            Vector3 trianglePoint;
            //SampleTrianglePoint(p0, p1, p2, rs.Get2D(threadId), lightPointNormal, trianglePoint, triPdf);
            Vector3 wi;
            Vector2 uv = new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
            float triPdf = 0.0f;
            Vector3 Li = SampleTriangleLightRadiance(p0, p1, p2, uv, new Vector3(-1.8f, 2.7f, 2.2f), Vector3.up, gpuLight, out wi, out trianglePoint, out triPdf);
            lightPdf *= triPdf;
        }
    }
    */

    void FilterSampleTesting(Filter filter)
    {
        //GPUFilterSample uv = filter.Sample(MathUtil.GetRandom01());
        GPUFilterSample uv = filter.Sample(new Vector2(0.0546875000f, 0.802469254f));
        Debug.Log(uv.p);
        uv = filter.Sample(new Vector2(0.0859375000f, 0.0246913619f));
        Debug.Log(uv.p);

        Vector2Int filterSize = filter.GetDistributionSize();

        List<Vector2> marginal = filter.GetGPUMarginalDistributions();

        List<Vector2> conditional = filter.GetGPUConditionalDistributions();

        List<float> conditionalFuncInts = filter.SampleDistributions().GetGPUConditionFuncInts();

        GPUDistributionDiscript discript = new GPUDistributionDiscript();
        discript.start = 0;
        discript.num = filterSize.y;
        discript.unum = filterSize.x;
        Bounds2D domain = filter.GetDomain();
        discript.domain = new Vector4(domain.min[0], domain.max[0], domain.min[1], domain.max[1]);
        float pdf = 0;
        Vector2 testSample = RayTracingTest.SampleDistribution2DContinous(new Vector2(0.0859375000f, 0.0246913619f), discript, marginal,
            conditional, conditionalFuncInts, out pdf);
        Debug.Log(testSample);
    }
}
