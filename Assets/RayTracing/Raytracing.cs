using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;


//[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class Raytracing : MonoBehaviour
{
    public RaytracingData _RayTracingData;
    public TracingKernel _RaytracingKernel;
    public WavefrontResource wavefrontResource;
    public MegaKernelResource megaResource;
    private Camera cameraComponent;
    public Material _BlitMaterial;
    private int cullingMask = 0;

    void Start()
    {
        if (_RayTracingData._kernelType == RaytracingData.KernelType.Wavefront)
        {
            //WavefrontResource wavefrontResource = new WavefrontResource();
            _RaytracingKernel = new WavefrontKernel(wavefrontResource);
        }
        else if (_RayTracingData._kernelType == RaytracingData.KernelType.Mega)
        {
            _RaytracingKernel = new MegaKernel(megaResource);
        }

        cameraComponent = GetComponent<Camera>();
        _RaytracingKernel.Setup(cameraComponent, _RayTracingData);

        if (_BlitMaterial == null)
        {
            Shader blitShader = Shader.Find("RayTracing/Blit");
            if (blitShader != null)
                _BlitMaterial = new Material(blitShader);

            _BlitMaterial.SetInt("_HDRType", (int)_RayTracingData.HDR);
        }

        cullingMask = cameraComponent.cullingMask;
        cameraComponent.cullingMask = 0;
    }

    void Update()
    {
        _RaytracingKernel.Update(cameraComponent);
        if (_BlitMaterial != null)
        {
            _BlitMaterial.SetInt("_HDRType", (int)_RayTracingData.HDR);
        }

        if (Input.GetMouseButtonUp(0))
        {
            Vector3 radiance = RayTracingTest.OnePathTracing((int)Input.mousePosition.x, (int)Input.mousePosition.y, (int)Screen.width, 1, 
                _RaytracingKernel.GetGPUSceneData(), _RaytracingKernel.GetGPUFilterData().filter, cameraComponent);
            Vector3 K = new Vector3(3.9747f, 2.38f, 1.5998f);
            Vector3 etaT = new Vector3(0.1428f, 0.3741f, 1.4394f);
            float cosTheta = 0.3f;
            Vector3 fr = RayTracingTest.FrConductor(cosTheta, Vector3.one, etaT, K);
            Debug.Log("cosTheta = " + cosTheta + " fresnel = " + fr);
        }
    }

    void OnDestroy()
    {
        _RaytracingKernel.Release();
        MeshRenderer[] meshRenderers = GameObject.FindObjectsOfType<MeshRenderer>();
        cameraComponent.cullingMask = cullingMask;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        //Graphics.Blit(outputTexture, destination);
        //for gbuffer test
        if (_BlitMaterial != null)
            Graphics.Blit(_RaytracingKernel.GetOutputTexture(), destination, _BlitMaterial);
        else
            Graphics.Blit(_RaytracingKernel.GetOutputTexture(), destination);
    }

    private void OnGUI()
    {
        
    }


    Vector3 MinOrMax(GPUBounds box, int n)
    {
        return n == 0 ? box.min : box.max;
    }
    Vector3 Corner(GPUBounds box, int n)
    {
        return new Vector3(MinOrMax(box, n & 1).x,
            MinOrMax(box, (n & 2) > 0 ? 1 : 0).y,
            MinOrMax(box, (n & 4) > 0 ? 1 : 0).z);
    }
    bool BoundIntersectP(GPURay ray, GPUBounds bounds, Vector3 invDir, int[] dirIsNeg)
    {
        // Check for ray intersection against $x$ and $y$ slabs
        float tMin = (MinOrMax(bounds, dirIsNeg[0]).x - ray.orig.x) * invDir.x;
        float tMax = (MinOrMax(bounds, 1 - dirIsNeg[0]).x - ray.orig.x) * invDir.x;
        float tyMin = (MinOrMax(bounds, dirIsNeg[1]).y - ray.orig.y) * invDir.y;
        Vector3 corner4 = MinOrMax(bounds, 1 - dirIsNeg[1]);
        float tyMax = (MinOrMax(bounds, 1 - dirIsNeg[1]).y - ray.orig.y) * invDir.y;

        // Update _tMax_ and _tyMax_ to ensure robust bounds intersection
        //tMax *= 1 + 2 * gamma(3);
        //tyMax *= 1 + 2 * gamma(3);
        if (tMin > tyMax || tyMin > tMax)
            return false;
        if (tyMin > tMin)
            tMin = tyMin;
        if (tyMax < tMax)
            tMax = tyMax;

        // Check for ray intersection against $z$ slab
        float tzMin = (MinOrMax(bounds, dirIsNeg[2]).z - ray.orig.z) * invDir.z;
        float tzMax = (MinOrMax(bounds, 1 - dirIsNeg[2]).z - ray.orig.z) * invDir.z;

        // Update _tzMax_ to ensure robust bounds intersection
        //tzMax *= 1 + 2 * gamma(3);
        if (tMin > tzMax || tzMin > tMax)
            return false;
        if (tzMin > tMin) tMin = tzMin;
        if (tzMax < tMax) tMax = tzMax;
        return (tMin < ray.tMax) && (tMax > 0);
    }

    bool IntersectRay(GPUBounds bounds, GPURay ray)
    {
        Bounds unityBounds = new Bounds();
        unityBounds.SetMinMax(bounds.min, bounds.max);
        Ray unityRay = new Ray();
        unityRay.origin = ray.orig;
        unityRay.direction = ray.direction;
        return unityBounds.IntersectRay(unityRay);
    }
}
