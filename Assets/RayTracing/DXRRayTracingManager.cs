using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class DXRRayTracingManager : Singleton<DXRRayTracingManager>
{
    public UnityEngine.Rendering.RayTracingAccelerationStructure rtas = null;
    private UnityEngine.Rendering.RayTracingShader pathTracing = null;
    private Renderer[] renderers = null;

    public void Setup(DXRPTResource resource)
    {
        pathTracing = resource.pathTracing;
        renderers = GameObject.FindObjectsOfType<MeshRenderer>();
    }

    // Update is called once per frame
    public void Update(Camera camera)
    {
        if (rtas == null)
        {
            rtas = new UnityEngine.Rendering.RayTracingAccelerationStructure();
        }
        else
        {
            rtas.ClearInstances();
        }
            
        rtas.Build();
        
    }

    public void Release()
    {
        if (rtas != null)
        {
            rtas.Release();
        }
    }
}
