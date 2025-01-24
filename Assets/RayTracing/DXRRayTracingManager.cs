using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class DXRRayTracingManager : Singleton<DXRRayTracingManager>
{
    public RayTracingAccelerationStructure rtas = null;
    private RayTracingShader pathTracing = null;
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
            rtas = new RayTracingAccelerationStructure();
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
