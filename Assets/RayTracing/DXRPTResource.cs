using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[System.Serializable]
[CreateAssetMenu(menuName = "Raytracing/DXR Raytracing Resource")]
public class DXRPTResource : ScriptableObject
{
    public RayTracingShader pathTracing;
    public ComputeShader InitRandom;
}
