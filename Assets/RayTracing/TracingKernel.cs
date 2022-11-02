using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

[Serializable]
public class RaytracingData
{
    public enum TracingView
    {
        ColorView,
        NormalView,
        DepthView,
        MipmapView,
        GBufferView,
        ShadowRayView,
        FresnelView,
        EnvmapUVView,
    }

    public enum KernelType
    {
        Mega,
        Wavefront,
    }

    public enum HDRType
    {
        Default,
        Filmic,
        ACE,
    }

    public KernelType _kernelType = KernelType.Wavefront;

    public TracingView viewMode = TracingView.ColorView;

    public int SamplesPerPixel = 128;
    public int MaxDepth = 5;
    public int MinDepth = 3;
    public FilterType filterType = FilterType.Gaussian;
    public Vector2 fiterRadius = Vector2.one;
    public float gaussianSigma = 0.5f;
    public HDRType HDR = HDRType.Default;
    public float _Exposure = 1;
    public bool _EnviromentMapEnable = true;
    public bool _UniformSampleLight = false;
    //public bool _UseBVHPlugin = true;
    public bool _SaveOutputTexture = false;
    //depth of field params
    public float _LensRadius = 0;
    public float _FocalLength = 1;
}



public interface TracingKernel
{
    IEnumerator Setup(Camera camera, RaytracingData data);
    bool Update(Camera camera);

    void Release();

    RenderTexture GetOutputTexture();

    GPUSceneData GetGPUSceneData();

    GPUFilterData GetGPUFilterData();

    int GetCurrentSPPCount();
}




