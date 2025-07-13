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
        RNGView,
    }

    public enum KernelType
    {
        Mega,
        Wavefront,
        DXR,
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
    [Range(0f, 1f)]
    public float _EnvironmentLightPmf = 0.5f;
    //public bool _UseBVHPlugin = true;
    public bool _SaveOutputTexture = false;
    //depth of field params
    public float _LensRadius = 0;
    public float _FocalLength = 1;
    public bool RestirEnable = false;

    private RenderTexture outputTexture;
    private RenderTexture rayConeGBuffer;
    private RenderTexture spectrumsBuffer;

    public RenderTexture OutputTexture
    {
        get { return outputTexture; }
    }
    public RenderTexture RayConeGBuffer
    { 
        get { return rayConeGBuffer; } 
    }

    public RenderTexture SpectrumBuffer
    {
        get { return spectrumsBuffer; }
    }

    public void Initialize()
    {
        if (outputTexture == null)
        {
            outputTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf, 0);
            outputTexture.name = "FinalOutput";
            outputTexture.enableRandomWrite = true;
            outputTexture.Create();
        }

        if (rayConeGBuffer == null)
        {
            rayConeGBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            rayConeGBuffer.name = "RayConeGBuffer";
            rayConeGBuffer.enableRandomWrite = true;
            rayConeGBuffer.Create();
        }

        if (spectrumsBuffer == null)
        {
            spectrumsBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, 0);
            spectrumsBuffer.name = "Spectrum";
            spectrumsBuffer.enableRandomWrite = true;
            spectrumsBuffer.Create();
        }
    }

    public void Release()
    {
        if (outputTexture != null)
        {
            outputTexture.Release();
            Object.Destroy(outputTexture);
            outputTexture = null;
        }

        if (rayConeGBuffer != null)
        {
            rayConeGBuffer.Release();
            Object.Destroy(rayConeGBuffer);
            rayConeGBuffer = null;
        }

        if (spectrumsBuffer != null)
        {
            spectrumsBuffer.Release();
            Object.Destroy(spectrumsBuffer);
            spectrumsBuffer = null;
        }
    }
}



public interface TracingKernel
{
    IEnumerator Setup(Camera camera, RaytracingData data);
    bool Update(Camera camera);

    void Release();

    GPUSceneData GetGPUSceneData();

    GPUFilterData GetGPUFilterData();

    int GetCurrentSPPCount();
}




