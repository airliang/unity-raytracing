using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[System.Serializable]
[CreateAssetMenu(menuName = "Raytracing/Restir Resource")]
public class RestirResource : ScriptableObject
{
    public RayTracingShader GenerateSamples;
    public ComputeShader SpatialReuse;
    public RayTracingShader PixelShading;
}
