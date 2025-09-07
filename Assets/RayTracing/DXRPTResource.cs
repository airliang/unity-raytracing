using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
[CreateAssetMenu(menuName = "Raytracing/DXR Raytracing Resource")]
public class DXRPTResource : ScriptableObject
{
    public UnityEngine.Rendering.RayTracingShader pathTracing;
    public ComputeShader InitRandom;
}
