using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class RestirKernel : TracingKernel
{
    RaytracingData _rayTracingData;
    private const int k_MaxNumSubMeshes = 32;
    public UnityEngine.Rendering.RayTracingAccelerationStructure rtas = null;
    private UnityEngine.Rendering.RayTracingShader generateReservoirSamples = null;

    public RestirKernel(RestirResource restirResource)
    {
        generateReservoirSamples = restirResource.GenerateSamples;
    }

    public IEnumerator Setup(Camera camera, RaytracingData data)
    {
        yield return null;
    }
    public bool Update(Camera camera)
    {
        return true;
    }

    public void Release()
    {

    }

    public GPUSceneData GetGPUSceneData()
    {
        return null;
    }

    public GPUFilterData GetGPUFilterData()
    {
        return null;
    }

    public int GetCurrentSPPCount()
    {
        return 0;
    }
}
