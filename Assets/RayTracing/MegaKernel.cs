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

    private RenderTexture outputTexture;
    RenderTexture imageSpectrumsBuffer;
    private ComputeShader _MegaCompute;
    ComputeShader _InitSampler;
    private int _MegaComputeKernel = -1;
    int _InitSamplerKernel = -1;
    RenderTexture rayConeGBuffer;
    ComputeBuffer samplerBuffer;

    //screen is [-1,1]
    //Matrix4x4 RasterToScreen;
    //Matrix4x4 RasterToCamera;
    //Matrix4x4 WorldToRaster;

    private MeshRenderer[] meshRenderers = null;

    Material gBufferMaterial = null;
    private CommandBuffer renderGBufferCmd;

    //int samplesPerPixel = 128;

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

    public RenderTexture GetOutputTexture()
    {
        return outputTexture;
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

        ReleaseRenderTexture(imageSpectrumsBuffer);
    }

    public IEnumerator Setup(Camera camera, RaytracingData data)
    {
        while (RaytracingStates.states != RaytracingStates.States.Rendering)
        {
            if (RaytracingStates.states == RaytracingStates.States.SceneLoading)
            {
                _rayTracingData = data;

                if (outputTexture == null)
                {
                    outputTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf, 0);
                    outputTexture.enableRandomWrite = true;
                }

                if (imageSpectrumsBuffer == null)
                {
                    imageSpectrumsBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, 0);
                    imageSpectrumsBuffer.enableRandomWrite = true;
                }

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
        RenderToGBuffer(camera);
        _MegaCompute.SetMatrix("RasterToCamera", gpuSceneData.RasterToCamera);
        _MegaCompute.SetMatrix("CameraToWorld", camera.cameraToWorldMatrix);
        _MegaCompute.SetInt("framesNum", framesNum);
        _MegaCompute.Dispatch(_MegaComputeKernel, threadGroupX, threadGroupY, 1);
        return false;
    }

    private void RenderToGBuffer(Camera camera)
    {
        //ScriptableCullingParameters parameters = new ScriptableCullingParameters();
        //camera.TryGetCullingParameters(out parameters);

        if (rayConeGBuffer == null)
        {
            rayConeGBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            rayConeGBuffer.name = "RayConeGBuffer";
            rayConeGBuffer.enableRandomWrite = true;
        }


        if (gBufferMaterial == null)
        {
            Shader renderToGBuffer = Shader.Find("RayTracing/RayCone");
            gBufferMaterial = new Material(renderToGBuffer);
        }

        if (renderGBufferCmd == null)
        {
            renderGBufferCmd = new CommandBuffer();
            renderGBufferCmd.name = "RayConeGBuffer Commands";
        }
        CommandBuffer cmd = renderGBufferCmd;//new CommandBuffer();
        cmd.Clear();
        cmd.BeginSample("Render GBuffer");
        cmd.SetRenderTarget(rayConeGBuffer);
        cmd.ClearRenderTarget(true, true, Color.black);
        cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
        cmd.SetViewport(new Rect(0, 0, (float)Screen.width, (float)Screen.height));


        Plane[] frustums = GeometryUtility.CalculateFrustumPlanes(camera);
        for (int i = 0; i < meshRenderers.Length; ++i)
        {
            if (GeometryUtility.TestPlanesAABB(frustums, meshRenderers[i].bounds))
                cmd.DrawRenderer(meshRenderers[i], gBufferMaterial);
        }
        cmd.EndSample("Render GBuffer");
        Graphics.ExecuteCommandBuffer(cmd);

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
        _InitSampler.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        _InitSampler.Dispatch(_InitSamplerKernel, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);

        _MegaCompute.SetInt("MAX_PATH", _rayTracingData.MaxDepth);
        _MegaCompute.SetInt("MIN_PATH", _rayTracingData.MinDepth);
        _MegaCompute.SetBuffer(_MegaComputeKernel, "RNGs", samplerBuffer);

        _MegaCompute.SetTexture(_MegaComputeKernel, "outputTexture", outputTexture);
        _MegaCompute.SetTexture(_MegaComputeKernel, "spectrums", imageSpectrumsBuffer);
        gpuSceneData.SetComputeShaderGPUData(_MegaCompute, _MegaComputeKernel);
        gpuFilterData.SetComputeShaderGPUData(_MegaCompute, _MegaComputeKernel);

        _MegaCompute.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        //_MegaCompute.SetMatrix("RasterToCamera", RasterToCamera);
        _MegaCompute.SetMatrix("CameraToWorld", camera.cameraToWorldMatrix);
        //_MegaCompute.SetFloat("cameraConeSpreadAngle", cameraConeSpreadAngle);
        _MegaCompute.SetInt("debugView", (int)_rayTracingData.viewMode);
        _MegaCompute.SetFloat("cameraFar", camera.farClipPlane);
        _MegaCompute.SetFloat("_LensRadius", _rayTracingData._LensRadius);
        _MegaCompute.SetFloat("_FocalLength", _rayTracingData._FocalLength);

        if (rayConeGBuffer == null)
        {
            rayConeGBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RGHalf);
            rayConeGBuffer.name = "RayConeGBuffer";
            rayConeGBuffer.enableRandomWrite = true;
        }
        _MegaCompute.SetTexture(_MegaComputeKernel, "RayConeGBuffer", rayConeGBuffer);

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
