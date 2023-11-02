using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
[CreateAssetMenu(menuName = "Raytracing/Wavefront Kernel Resource")]
public class WavefrontResource : ScriptableObject
{
    //[Reload("RayTracing/Shaders/GenerateRay.compute")]
    public ComputeShader generateRay;
    //[Reload("RayTracing/Shaders/RayTravel.compute")]
    public ComputeShader RayTravel;
    //[Reload("RayTracing/Shaders/InitSampler.compute")]
    public ComputeShader InitRandom;
    //[Reload("RayTracing/Shaders/ResetRayQueues.compute")]
    public ComputeShader ResetRayQueues;
    //[Reload("RayTracing/Shaders/RayMiss.compute")]
    public ComputeShader RayMiss;
    //[Reload("RayTracing/Shaders/HitAreaLight.compute")]
    public ComputeShader HitAreaLight;
    //[Reload("RayTracing/Shaders/EstimateDirect.compute")]
    public ComputeShader EstimateDirect;
    //[Reload("RayTracing/Shaders/ShadowRayLighting.compute")]
    public ComputeShader ShadowRayLighting;
    //[Reload("RayTracing/Shaders/RayQueueClear.compute")]
    public ComputeShader RayQueueClear;
    //[Reload("RayTracing/Shaders/ImageReconstruction.compute")]
    public ComputeShader ImageReconstruction;
    //[Reload("RayTracing/Shaders/TracingDebug.compute")]
    public ComputeShader DebugView;

    //[Reload("RayTracing/Shaders/RayCone.shader")]
    public Shader RayCone;
}
