using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

public class MegaKernel : TracingKernel
{
    RaytracingData _rayTracingData;
    GPUSceneData gpuSceneData;
    GPUFilterData gpuFilterData;

    private ComputeShader _MegaCompute;
    ComputeShader _InitSampler;
    private int _MegaComputeKernel = -1;
    int _InitSamplerKernel = -1;
    ComputeBuffer samplerBuffer;

    private MeshRenderer[] meshRenderers = null;

    int framesNum = 0;
    
    float executeTimeBegin = 0;

    public MegaKernel(MegaKernelResource resource)
    {
        _MegaCompute = resource.MegaKernel;
        _MegaComputeKernel = _MegaCompute.FindKernel("CSMain");
        _InitSampler = resource.InitSampler;
        _InitSamplerKernel = _InitSampler.FindKernel("CSInitSampler");
    }

    public GPUSceneData GetGPUSceneData()
    {
        return gpuSceneData;
    }

    public GPUFilterData GetGPUFilterData()
    {
        return gpuFilterData;
    }

    public void Release()
    {
        void ReleaseComputeBuffer(ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }

        void ReleaseRenderTexture(RenderTexture texture)
        {
            if (texture != null)
            {
                texture.Release();
                Object.Destroy(texture);
                texture = null;
            }
        }

        if (gpuSceneData != null)
            gpuSceneData.Release();

        if (gpuFilterData != null)
            gpuFilterData.Release();

        ReleaseComputeBuffer(samplerBuffer);
    }

    public IEnumerator Setup(Camera camera, RaytracingData data)
    {
        while (RaytracingStates.states != RaytracingStates.States.Rendering)
        {
            if (RaytracingStates.states == RaytracingStates.States.SceneLoading)
            {
                _rayTracingData = data;

                gpuSceneData = new GPUSceneData(data._UniformSampleLight, data._EnviromentMapEnable, true);
                meshRenderers = GameObject.FindObjectsOfType<MeshRenderer>();
                yield return gpuSceneData.Setup(meshRenderers, camera);
            }

            if (RaytracingStates.states == RaytracingStates.States.PrepareRendering)
            {

                gpuFilterData = new GPUFilterData();
                Filter filter = null;
                if (data.filterType == FilterType.Gaussian)
                {
                    filter = new GaussianFilter(data.fiterRadius, data.gaussianSigma);
                }
                gpuFilterData.Setup(filter);

                SetupMegaCompute(camera);
                RaytracingStates.states = RaytracingStates.States.Rendering;
                yield return null;
            }
        }
    }

    public bool Update(Camera camera)
    {
        //if (!gpuSceneData.IsRunalbe())
        //    return;
        if (framesNum == 0)
        {
            executeTimeBegin = Time.realtimeSinceStartup;
        }

        if (framesNum++ >= _rayTracingData.SamplesPerPixel)
        {
            if (framesNum == _rayTracingData.SamplesPerPixel + 1)
            {
                float timeInterval = Time.realtimeSinceStartup - executeTimeBegin;
                Debug.Log("Megakernel GPU rendering finished, cost time:" + timeInterval);
            }
            return true;
        }
        //_InitSampler.Dispatch(_InitSamplerKernel, (int)Screen.width / 8 + 1, (int)Screen.height / 8 + 1, 1);
        int threadGroupX = Screen.width / 8 + ((Screen.width % 8) != 0 ? 1 : 0);
        int threadGroupY = Screen.height / 8 + ((Screen.height % 8) != 0 ? 1 : 0);
        //RenderToGBuffer(camera);
        _MegaCompute.SetMatrix(PathTracingParam._RasterToCamera, gpuSceneData.RasterToCamera);
        _MegaCompute.SetMatrix(PathTracingParam._CameraToWorld, camera.cameraToWorldMatrix);
        _MegaCompute.SetInt(PathTracingParam._FrameIndex, framesNum);
        _MegaCompute.Dispatch(_MegaComputeKernel, threadGroupX, threadGroupY, 1);
        return false;
    }

    void SetupMegaCompute(Camera camera)
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;

        if (samplerBuffer == null)
        {
            samplerBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(uint), ComputeBufferType.Structured);
        }
        _InitSampler.SetBuffer(_InitSamplerKernel, "RNGs", samplerBuffer);
        _InitSampler.SetVector(PathTracingParam._ScreenSize, new Vector4(rasterWidth, rasterHeight, 0, 0));
        _InitSampler.Dispatch(_InitSamplerKernel, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);

        _MegaCompute.SetInt("MAX_PATH", _rayTracingData.MaxDepth);
        _MegaCompute.SetInt("MIN_PATH", _rayTracingData.MinDepth);
        _MegaCompute.SetBuffer(_MegaComputeKernel, "RNGs", samplerBuffer);

        _MegaCompute.SetTexture(_MegaComputeKernel, "outputTexture", _rayTracingData.OutputTexture);
        _MegaCompute.SetTexture(_MegaComputeKernel, "spectrums", _rayTracingData.SpectrumBuffer);
        gpuSceneData.SetComputeShaderGPUData(_MegaCompute, _MegaComputeKernel);
        gpuFilterData.SetComputeShaderGPUData(_MegaCompute, _MegaComputeKernel);

        _MegaCompute.SetVector(PathTracingParam._ScreenSize, new Vector4(rasterWidth, rasterHeight, 0, 0));
        //_MegaCompute.SetMatrix("RasterToCamera", RasterToCamera);
        _MegaCompute.SetMatrix(PathTracingParam._CameraToWorld, camera.cameraToWorldMatrix);
        //_MegaCompute.SetFloat("cameraConeSpreadAngle", cameraConeSpreadAngle);
        _MegaCompute.SetInt(PathTracingParam._DebugView, (int)_rayTracingData.viewMode);
        _MegaCompute.SetFloat("cameraFar", camera.farClipPlane);
        _MegaCompute.SetFloat(PathTracingParam._LensRadius, _rayTracingData._LensRadius);
        _MegaCompute.SetFloat(PathTracingParam._FocalLength, _rayTracingData._FocalLength);

        _MegaCompute.SetTexture(_MegaComputeKernel, "RayConeGBuffer", _rayTracingData.RayConeGBuffer);

        SetTextures(_MegaCompute, _MegaComputeKernel);
        
        _MegaCompute.SetFloat("_Exposure", _rayTracingData._Exposure);
    }

    void SetTextures(ComputeShader cs, int kernel)
    {
        cs.SetTexture(kernel, "albedoTexArray", RayTracingTextures.Instance.GetAlbedo2DArray(128));
        cs.SetTexture(kernel, "normalTexArray", RayTracingTextures.Instance.GetNormal2DArray(128));
    }

    public int GetCurrentSPPCount()
    {
        return framesNum;
    }
}
