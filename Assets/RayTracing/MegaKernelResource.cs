using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Raytracing/Mega Kernel Resource")]
public class MegaKernelResource : ScriptableObject
{
    //[Reload("RayTracing/Shaders/MegaKernel.compute")] 
    public ComputeShader MegaKernel;
    //[Reload("RayTracing/Shaders/InitSampler.compute")]
    public ComputeShader InitSampler;
    //[Reload("RayTracing/Shaders/RayCone.shader")]
    public Shader RayCone;
}
