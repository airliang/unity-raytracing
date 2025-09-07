using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
[CreateAssetMenu(menuName = "Raytracing/Restir Resource")]
public class RestirResource : ScriptableObject
{
    public UnityEngine.Rendering.RayTracingShader GenerateSamples;
    public ComputeShader SpatialReuse;
    public UnityEngine.Rendering.RayTracingShader PixelShading;
}
