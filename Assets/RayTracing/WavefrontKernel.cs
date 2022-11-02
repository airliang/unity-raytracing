using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;


[Serializable]
public class WavefrontKernel : TracingKernel
{
    private RaytracingData _rayTracingData;
    GPUSceneData gpuSceneData;
    GPUFilterData gpuFilterData;

    ComputeShader generateRay;
    ComputeShader ResetRayQueues;
    ComputeShader RayTravel;
    ComputeShader InitRandom;
    ComputeShader RayMiss;
    ComputeShader HitAreaLight;
    ComputeShader RayQueueClear;
    //ComputeShader SampleShadowRay;
    ComputeShader EstimateDirect;
    ComputeShader ShadowRayLighting;
    ComputeShader ImageReconstruction;
    ComputeShader DebugView;
    int kGeneratePrimaryRay = -1;
    int kResetRayQueues = -1;
    int kRayTraversal = -1;
    int kRayMiss = -1;
    int kHitAreaLight = -1;
    int kInitRandom = -1;
    //int kSampleShadowRay = -1;
    int kEstimateDirect = -1;
    int kShadowRayLighting = -1;
    int kRayQueueClear = -1;
    int kImageReconstruction = -1;
    int kDebugView = -1;

    private CommandBuffer renderGBufferCmd;
    public float _Exposure = 1;

    //ComputeBuffer rayBuffer;
    ComputeBuffer workItemBuffer;
    ComputeBuffer samplerBuffer;
    ComputeBuffer pathRadianceBuffer;

    //ComputeBuffer shadowRayBuffer;
    RenderTexture imageSpectrumsBuffer;


    ComputeBuffer rayQueueSizeBuffer;
    ComputeBuffer rayQueueBuffer;
    ComputeBuffer nextRayQueueBuffer;
    ComputeBuffer escapeRayQueueBuffer;
    ComputeBuffer hitLightQueueBuffer;
    ComputeBuffer materialShadingQueueBuffer;
    ComputeBuffer shadowRayQueueBuffer;

    ComputeBuffer escapeRayItemBuffer;
    ComputeBuffer hitLightItemBuffer;
    ComputeBuffer materialShadingItemBuffer;
    ComputeBuffer shadowRayItemBuffer;

    RenderTexture outputTexture;
    RenderTexture rayConeGBuffer;

    //screen is [-1,1]
    //Matrix4x4 RasterToScreen;
    //Matrix4x4 RasterToCamera;
    //Matrix4x4 WorldToRaster;


    int MAX_PATH = 5;
    int MIN_PATH = 3;
    //int samplesPerPixel = 1024;


    int framesNum = 0;
    Material gBufferMaterial = null;
    //image pixel filter
    Filter filter;

    //float cameraConeSpreadAngle = 0;
    uint[] RayQueueSizeArray;
    //uint[] gpuRandomSamplers = null;

    private MeshRenderer[] meshRenderers = null;

    const int WorkItemSize = 64;
    const int EscapeQueueItemSize = 64;
    const int HitLightItemSize = 88;
    const int MaterialQueueItemSize = 56;
    const int ShadowRayItemSize = 48;

    float executeTimeBegin = 0;

    public WavefrontKernel(WavefrontResource resource)
    {
        generateRay = resource.generateRay;
        RayTravel = resource.RayTravel;
        InitRandom = resource.InitRandom;
        ResetRayQueues = resource.ResetRayQueues;
        RayMiss = resource.RayMiss;
        HitAreaLight = resource.HitAreaLight;
        //SampleShadowRay = resource.SampleShadowRay;
        EstimateDirect = resource.EstimateDirect;
        ShadowRayLighting = resource.ShadowRayLighting;
        ImageReconstruction = resource.ImageReconstruction;
        DebugView = resource.DebugView;
        
        RayQueueClear = resource.RayQueueClear;
    }

    public GPUSceneData GetGPUSceneData()
    {
        return gpuSceneData;
    }

    public GPUFilterData GetGPUFilterData()
    {
        return gpuFilterData;
    }

    public IEnumerator Setup(Camera camera, RaytracingData data)
    {
        while (RaytracingStates.states != RaytracingStates.States.Rendering)
        {
            if (RaytracingStates.states == RaytracingStates.States.SceneLoading)
            {
                _rayTracingData = data;
                MAX_PATH = data.MaxDepth;

                if (outputTexture == null)
                {
                    outputTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf, 0);
                    outputTexture.enableRandomWrite = true;
                }

                gpuSceneData = new GPUSceneData(data._UniformSampleLight, data._EnviromentMapEnable, true);
                meshRenderers = GameObject.FindObjectsOfType<MeshRenderer>();
                yield return gpuSceneData.Setup(meshRenderers, camera);
            }


            if (RaytracingStates.states == RaytracingStates.States.PrepareRendering)
            {
                gpuFilterData = new GPUFilterData();
                if (data.filterType == FilterType.Gaussian)
                {
                    filter = new GaussianFilter(data.fiterRadius, data.gaussianSigma);
                }
                gpuFilterData.Setup(filter);

                RayQueueSizeArray = new uint[MAX_PATH * 5];
                for (int i = 0; i < RayQueueSizeArray.Length; ++i)
                {
                    RayQueueSizeArray[i] = 0;
                }

                SetupSamplers();

                SetupGPUBuffers();

                //generate ray
                //init the camera parameters
                Profiler.BeginSample("SetupGenerateRay");
                SetupGenerateRay(camera);
                Profiler.EndSample();

                Profiler.BeginSample("SetupResetRayQueues");
                SetupResetRayQueues();
                Profiler.EndSample();

                Profiler.BeginSample("SetupRayTraversal");
                SetupRayTraversal();
                Profiler.EndSample();

                SetupRayMiss();
                SetupHitAreaLight();

                Profiler.BeginSample("SetupEstimateDirect");
                SetupEstimateDirect();
                Profiler.EndSample();

                SetupShadowRayLighting();

                //SetupGeneratePath();
                Profiler.BeginSample("SetupImageReconstruction");
                SetupImageReconstruction();
                Profiler.EndSample();

                SetupRayQueueClear();

                RaytracingStates.states = RaytracingStates.States.Rendering;
                yield return null;
            }
        }
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

        void ReleaseTexture(Texture texture)
        {
            if (texture != null)
            {
                Object.Destroy(texture);
                texture = null;
            }
        }

        if (gpuSceneData != null)
            gpuSceneData.Release();

        if (gpuFilterData != null)
            gpuFilterData.Release();

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

        ReleaseComputeBuffer(samplerBuffer);
        ReleaseComputeBuffer(workItemBuffer);
        ReleaseComputeBuffer(pathRadianceBuffer);
        //ReleaseComputeBuffer(shadowRayBuffer);
        ReleaseComputeBuffer(rayQueueSizeBuffer);
        ReleaseComputeBuffer(rayQueueBuffer);
        ReleaseComputeBuffer(nextRayQueueBuffer);
        ReleaseComputeBuffer(escapeRayQueueBuffer);
        ReleaseComputeBuffer(hitLightQueueBuffer);
        ReleaseComputeBuffer(materialShadingQueueBuffer);
        ReleaseComputeBuffer(shadowRayQueueBuffer);
        ReleaseComputeBuffer(escapeRayItemBuffer);
        ReleaseComputeBuffer(hitLightItemBuffer);
        ReleaseComputeBuffer(materialShadingItemBuffer);
        ReleaseComputeBuffer(shadowRayItemBuffer);
    }

    public bool Update(Camera camera)
    {
        if (framesNum == 0)
        {
            executeTimeBegin = Time.realtimeSinceStartup;
        }

        if (framesNum++ >= _rayTracingData.SamplesPerPixel)
        {
            //GPUFilterSample uv = filter.Sample(MathUtil.GetRandom01());
            //Debug.Log(uv.p);
            if (framesNum == _rayTracingData.SamplesPerPixel + 1)
            {
                float timeInterval = Time.realtimeSinceStartup - executeTimeBegin;
                Debug.Log("Wavefront GPU rendering finished, cost time:" + timeInterval);
            }
            return true;
        }

        RenderToGBuffer(camera);

        rayQueueSizeBuffer.SetData(RayQueueSizeArray);

        int rasterWidth = Screen.width;
        int rasterHeight = Screen.height;
        if (generateRay != null)
            generateRay.SetFloat("_time", Time.time);

        int curRaySizeIndex = 0;
        int nextRaySizeIndex = 1;

        generateRay.SetMatrix("RasterToCamera", gpuSceneData.RasterToCamera);
        generateRay.SetMatrix("CameraToWorld", camera.cameraToWorldMatrix);

        int threadGroupX = Screen.width / 8 + ((Screen.width % 8) != 0 ? 1 : 0);
        int threadGroupY = Screen.height / 8 + ((Screen.height % 8) != 0 ? 1 : 0);
        Profiler.BeginSample("GeneratePrimaryRay");
        generateRay.Dispatch(kGeneratePrimaryRay, threadGroupX, threadGroupY, 1);
        Profiler.EndSample();
        ComputeBuffer curRayQueue = rayQueueBuffer;
        ComputeBuffer nextRayQueue = nextRayQueueBuffer;

        void SwitchRayQueue()
        {
            ComputeBuffer tmpBuffer = nextRayQueue;
            nextRayQueue = curRayQueue;
            curRayQueue = tmpBuffer;
            int tmpIndex = nextRaySizeIndex;
            nextRaySizeIndex = curRaySizeIndex;
            curRaySizeIndex = tmpIndex;
        }

        ResetRayQueues.Dispatch(kResetRayQueues, 1, 1, 1);
        if (_rayTracingData.viewMode == RaytracingData.TracingView.ColorView)
        {
            for (int i = 0; true; ++i)
            {
                Profiler.BeginSample("ResetQueues");
                ResetQueues(nextRaySizeIndex);
                Profiler.EndSample();

                Profiler.BeginSample("Ray Cast");
                RayTravel.SetInt("bounces", i);
                RayTravel.SetInt("curQueueSizeIndex", curRaySizeIndex);
                RayTravel.SetBuffer(kRayTraversal, "_RayQueue", curRayQueue);
                RayTravel.Dispatch(kRayTraversal, threadGroupX, threadGroupY, 1);
                Profiler.EndSample();

                Profiler.BeginSample("Ray Miss Process");
                RayMiss.SetInt("bounces", i);
                RayMiss.Dispatch(kRayMiss, threadGroupX, threadGroupY, 1);
                Profiler.EndSample();

                //HitAreaLight.SetInt("bounces", i);
                Profiler.BeginSample("HitAreaLight Process");
                HitAreaLight.Dispatch(kHitAreaLight, threadGroupX, threadGroupY, 1);
                Profiler.EndSample();

                if (i == MAX_PATH)
                    break;


                Profiler.BeginSample("EstimateDirect Material Shading Process");
                EstimateDirect.SetInt("bounces", i);
                //EstimateDirect.SetInt("curQueueSizeIndex", curRaySizeIndex);
                EstimateDirect.SetInt("nextQueueSizeIndex", nextRaySizeIndex);
                EstimateDirect.SetBuffer(kEstimateDirect, "_NextRayQueue", nextRayQueue);
                EstimateDirect.Dispatch(kEstimateDirect, threadGroupX, threadGroupY, 1);
                Profiler.EndSample();

                Profiler.BeginSample("ShadowRayLighting Process");
                ShadowRayLighting.Dispatch(kShadowRayLighting, threadGroupX, threadGroupY, 1);
                Profiler.EndSample();
                //clear shadow ray queue
                SwitchRayQueue();
            }

            ImageReconstruction.SetInt("framesNum", framesNum);
            ImageReconstruction.SetFloat("_Exposure", _Exposure);
            ImageReconstruction.Dispatch(kImageReconstruction, threadGroupX, threadGroupY, 1);
        }
        else
        {
            if (kDebugView < 0)
                SetupDebugView(camera);

            DebugView.SetInt("debugView", (int)_rayTracingData.viewMode);
            DebugView.SetInt("bounces", 0);

            DebugView.Dispatch(kDebugView, threadGroupX, threadGroupY, 1);
                
        }

        return false;
    }

    private void ResetQueues(int nextRaySizeIndex)
    {
        //we must clear the next queue before using it
        RayQueueClear.SetInt("clearQueueIndex", nextRaySizeIndex);
        RayQueueClear.Dispatch(kRayQueueClear, 1, 1, 1);
    }

    private void RenderToGBuffer(Camera camera)
    {
        if (rayConeGBuffer == null)
        {
            rayConeGBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RGHalf);
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

    void SetupSamplers()
    {
        if (samplerBuffer == null)
        {
            samplerBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(uint), ComputeBufferType.Structured);
        }
        //if (gpuRandomSamplers == null)
        //{
        //    gpuRandomSamplers = new uint[Screen.width * Screen.height];
        //    samplerBuffer.SetData(gpuRandomSamplers);
        //}

        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        kInitRandom = InitRandom.FindKernel("CSInitSampler");
        InitRandom.SetBuffer(kInitRandom, "RNGs", samplerBuffer);
        InitRandom.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        InitRandom.Dispatch(kInitRandom, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);
        //for test
        //samplerBuffer.GetData(gpuRandomSamplers);

        //kTestSampler = initRandom.FindKernel("CSTestSampler");
        //initRandom.SetBuffer(kTestSampler, "RNGs", samplerBuffer);
        //initRandom.Dispatch(kTestSampler, (int)rasterWidth / 8 + 1, (int)rasterHeight / 8 + 1, 1);
        //samplerBuffer.GetData(gpuRandomSamplers);
    }

    void SetupGPUBuffers()
    {
        //if (rayBuffer == null)
        //{
        //    rayBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPURay)), ComputeBufferType.Structured);
        //}

        if (workItemBuffer == null)
        {
            workItemBuffer = new ComputeBuffer(Screen.width * Screen.height, WorkItemSize, ComputeBufferType.Structured);
        }

        if (pathRadianceBuffer == null)
        {
            pathRadianceBuffer = new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUPathRadiance)), ComputeBufferType.Structured);
        }

        if (rayQueueBuffer == null)
        {
            rayQueueBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(int), ComputeBufferType.Structured);
        }

        if (rayQueueSizeBuffer == null)
        {
            rayQueueSizeBuffer = new ComputeBuffer(RayQueueSizeArray.Length, sizeof(uint), ComputeBufferType.Structured);
        }

        if (nextRayQueueBuffer == null)
        {
            nextRayQueueBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(int), ComputeBufferType.Structured);
        }

        if (escapeRayQueueBuffer == null)
        {
            escapeRayQueueBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(int), ComputeBufferType.Default);
        }

        if (hitLightQueueBuffer == null)
        {
            hitLightQueueBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(int), ComputeBufferType.Default);
        }

        if (materialShadingQueueBuffer == null)
        {
            materialShadingQueueBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(uint), ComputeBufferType.Default);
        }

        if (shadowRayQueueBuffer == null)
        {
            shadowRayQueueBuffer = new ComputeBuffer(Screen.width * Screen.height, sizeof(uint), ComputeBufferType.Default);
        }

        if (escapeRayItemBuffer == null)
        {
            escapeRayItemBuffer = new ComputeBuffer(Screen.width * Screen.height, EscapeQueueItemSize, ComputeBufferType.Default);
        }

        if (hitLightItemBuffer == null)
        {
            hitLightItemBuffer = new ComputeBuffer(Screen.width * Screen.height, HitLightItemSize, ComputeBufferType.Default);
        }

        if (materialShadingItemBuffer == null)
        {
            materialShadingItemBuffer = new ComputeBuffer(Screen.width * Screen.height, MaterialQueueItemSize, ComputeBufferType.Default);
        }

        if (shadowRayItemBuffer == null)
        {
            shadowRayItemBuffer = new ComputeBuffer(Screen.width * Screen.height, ShadowRayItemSize, ComputeBufferType.Default);
        }
    }

    void SetupGenerateRay(Camera camera)
    {
        //generate ray
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        //init the camera parameters

        kGeneratePrimaryRay = generateRay.FindKernel("GeneratePrimary");

        //generateRay.SetBuffer(kGeneratePrimaryRay, "Rays", rayBuffer);
        generateRay.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        generateRay.SetMatrix("RasterToCamera", gpuSceneData.RasterToCamera);
        generateRay.SetMatrix("CameraToWorld", camera.cameraToWorldMatrix);
        generateRay.SetFloat("_LensRadius", _rayTracingData._LensRadius);
        generateRay.SetFloat("_FocalLength", _rayTracingData._FocalLength);
        generateRay.SetBuffer(kGeneratePrimaryRay, "RNGs", samplerBuffer);
        generateRay.SetBuffer(kGeneratePrimaryRay, "pathRadiances", pathRadianceBuffer);
        generateRay.SetBuffer(kGeneratePrimaryRay, "_WorkQueueItems", workItemBuffer);
        gpuFilterData.SetComputeShaderGPUData(generateRay, kGeneratePrimaryRay);
        //generateRay.SetBuffer(kGeneratePrimaryRay, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        generateRay.SetBuffer(kGeneratePrimaryRay, "_RayQueue", rayQueueBuffer);
    }

    void SetupResetRayQueues()
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;

        kResetRayQueues = ResetRayQueues.FindKernel("CSMain");
        ResetRayQueues.SetBuffer(kResetRayQueues, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        ResetRayQueues.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
    }

    void SetupRayMiss()
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;

        kRayMiss = RayMiss.FindKernel("CSMain");
        RayMiss.SetBuffer(kRayMiss, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        RayMiss.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        RayMiss.SetBuffer(kRayMiss, "_EscapeRayItems", escapeRayItemBuffer);
        RayMiss.SetBuffer(kRayMiss, "_RayMissQueue", escapeRayQueueBuffer);
        RayMiss.SetBuffer(kRayMiss, "pathRadiances", pathRadianceBuffer);
        gpuSceneData.SetComputeShaderGPUData(RayMiss, kRayMiss);
    }

    void SetupHitAreaLight()
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;

        kHitAreaLight = RayMiss.FindKernel("CSMain");
        HitAreaLight.SetBuffer(kHitAreaLight, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        HitAreaLight.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        HitAreaLight.SetBuffer(kHitAreaLight, "_HitLightQueueItems", hitLightItemBuffer);
        HitAreaLight.SetBuffer(kHitAreaLight, "_HitLightQueue", hitLightQueueBuffer);
        HitAreaLight.SetBuffer(kHitAreaLight, "pathRadiances", pathRadianceBuffer);
        gpuSceneData.SetComputeShaderGPUData(HitAreaLight, kHitAreaLight);
    }

    void SetupEstimateDirect()
    {
        kEstimateDirect = EstimateDirect.FindKernel("CSMain");
        gpuSceneData.SetComputeShaderGPUData(EstimateDirect, kEstimateDirect);
        //EstimateDirect.SetBuffer(kEstimateDirect, "ShadowRays", shadowRayBuffer);
        //SetComputeBuffer(EstimateDirect, kEstimateDirect, "Rays", rayBuffer);
    
        EstimateDirect.SetBuffer(kEstimateDirect, "RNGs", samplerBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "pathRadiances", pathRadianceBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_RayQueue", rayQueueBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_NextRayQueue", nextRayQueueBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_MaterialQueueItem", materialShadingItemBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_MaterialShadingQueue", materialShadingQueueBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_ShadowRayQueue", shadowRayQueueBuffer);
        SetComputeBuffer(EstimateDirect, kEstimateDirect, "_ShadowRayQueueItems", shadowRayItemBuffer);
        SetComputeBuffer(EstimateDirect, kRayTraversal, "_WorkQueueItems", workItemBuffer);
        EstimateDirect.SetVector("rasterSize", new Vector4(Screen.width, Screen.height, 0, 0));
        //EstimateDirect.SetMatrix("WorldToRaster", WorldToRaster);
        SetTextures(EstimateDirect, kEstimateDirect);
        EstimateDirect.SetInt("MIN_DEPTH", _rayTracingData.MinDepth);
    }

    void SetupShadowRayLighting()
    {
        kShadowRayLighting = ShadowRayLighting.FindKernel("CSMain");
        gpuSceneData.SetComputeShaderGPUData(ShadowRayLighting, kShadowRayLighting);
        SetComputeBuffer(ShadowRayLighting, kShadowRayLighting, "pathRadiances", pathRadianceBuffer);
        SetComputeBuffer(ShadowRayLighting, kShadowRayLighting, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        SetComputeBuffer(ShadowRayLighting, kShadowRayLighting, "_ShadowRayQueue", shadowRayQueueBuffer);
        SetComputeBuffer(ShadowRayLighting, kShadowRayLighting, "_ShadowRayQueueItems", shadowRayItemBuffer);
        ShadowRayLighting.SetVector("rasterSize", new Vector4(Screen.width, Screen.height, 0, 0));
    }

    void SetupImageReconstruction()
    {
        if (imageSpectrumsBuffer == null)
        {
            imageSpectrumsBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, 0);//new ComputeBuffer(Screen.width * Screen.height, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector3)), ComputeBufferType.Structured);
            imageSpectrumsBuffer.enableRandomWrite = true;
            imageSpectrumsBuffer.filterMode = FilterMode.Point;
        }

        kImageReconstruction = ImageReconstruction.FindKernel("CSMain");
        ImageReconstruction.SetBuffer(kImageReconstruction, "pathRadiances", pathRadianceBuffer);
        //ImageReconstruction.SetBuffer(kImageReconstruction, "spectrums", imageSpectrumsBuffer);
        SetComputeTexture(ImageReconstruction, kImageReconstruction, "spectrums", imageSpectrumsBuffer);
        ImageReconstruction.SetTexture(kImageReconstruction, "outputTexture", outputTexture);
        ImageReconstruction.SetVector("rasterSize", new Vector4(Screen.width, Screen.height, 0, 0));
    }

    void SetComputeBuffer(ComputeShader cs, int kernel, string name, ComputeBuffer buffer)
    {
        if (cs != null && buffer != null)
        {
            cs.SetBuffer(kernel, name, buffer);
        }
    }

    void SetComputeTexture(ComputeShader cs, int kernel, string name, Texture texture)
    {
        if (cs != null && texture != null)
        {
            cs.SetTexture(kernel, name, texture);
        }
    }

    void SetTextures(ComputeShader cs, int kernel)
    {
        cs.SetTexture(kernel, "albedoTexArray", RayTracingTextures.Instance.GetAlbedo2DArray(128));
        cs.SetTexture(kernel, "normalTexArray", RayTracingTextures.Instance.GetNormal2DArray(128));
    }

    

    void SetupDebugView(Camera camera)
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        kDebugView = DebugView.FindKernel("CSMain");
        gpuSceneData.SetComputeShaderGPUData(DebugView, kDebugView);
        DebugView.SetBuffer(kDebugView, "_WorkQueueItems", workItemBuffer);
        DebugView.SetBuffer(kDebugView, "RNGs", samplerBuffer);
        DebugView.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));

        DebugView.SetMatrix("RasterToCamera", gpuSceneData.RasterToCamera);
        DebugView.SetMatrix("CameraToWorld", camera.cameraToWorldMatrix);
        DebugView.SetFloat("cameraFar", camera.farClipPlane);
        SetTextures(DebugView, kDebugView);
        DebugView.SetTexture(kDebugView, "outputTexture", outputTexture);

        if (rayConeGBuffer == null)
        {
            rayConeGBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBHalf);
            rayConeGBuffer.name = "RayConeGBuffer";
            rayConeGBuffer.enableRandomWrite = true;
        }

        DebugView.SetTexture(kDebugView, "RayConeGBuffer", rayConeGBuffer);
    }

    void SetupRayTraversal()
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        kRayTraversal = RayTravel.FindKernel("RayTraversal");
        gpuSceneData.SetComputeShaderGPUData(RayTravel, kRayTraversal);

        RayTravel.SetBuffer(kRayTraversal, "_WorkQueueItems", workItemBuffer);
        //RayTravel.SetBuffer(kRayTraversal, "RNGs", samplerBuffer);
        //RayTravel.SetBuffer(kRayTraversal, "pathRadiances", pathRadianceBuffer);
        RayTravel.SetBuffer(kRayTraversal, "_HitLightQueueItems", hitLightItemBuffer);
        RayTravel.SetBuffer(kRayTraversal, "_EscapeRayItems", escapeRayItemBuffer);
        RayTravel.SetBuffer(kRayTraversal, "_MaterialQueueItem", materialShadingItemBuffer);

        //setup rayqueues
        SetComputeBuffer(RayTravel, kRayTraversal, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        SetComputeBuffer(RayTravel, kRayTraversal, "_RayQueue", rayQueueBuffer);
        RayTravel.SetBuffer(kRayTraversal, "_NextRayQueue", nextRayQueueBuffer);
        RayTravel.SetBuffer(kRayTraversal, "_RayMissQueue", escapeRayQueueBuffer);
        RayTravel.SetBuffer(kRayTraversal, "_HitLightQueue", hitLightQueueBuffer);
        RayTravel.SetBuffer(kRayTraversal, "_MaterialShadingQueue", materialShadingQueueBuffer);
        RayTravel.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
        //RayTravel.SetFloat("cameraConeSpreadAngle", cameraConeSpreadAngle);
        SetTextures(RayTravel, kRayTraversal);

        if (rayConeGBuffer == null)
        {
            rayConeGBuffer = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.RGHalf);
            rayConeGBuffer.name = "RayConeGBuffer";
            rayConeGBuffer.enableRandomWrite = true;
        }
        RayTravel.SetTexture(kRayTraversal, "RayConeGBuffer", rayConeGBuffer);
        //RayTravel.SetTexture(kRayTraversal, "outputTexture", outputTexture);
    }

    void SetupRayQueueClear()
    {
        if (kRayQueueClear == -1)
        {
            kRayQueueClear = RayQueueClear.FindKernel("CSMain");
            RayQueueClear.SetBuffer(kRayQueueClear, "_RayQueueSizeBuffer", rayQueueSizeBuffer);
        }
    }

    public int GetCurrentSPPCount()
    {
        return framesNum;
    }
}
