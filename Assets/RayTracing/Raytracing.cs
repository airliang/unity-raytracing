using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.XR;
using System.IO;
using UnityEngine.SceneManagement;
using UnityEngine.Experimental.Rendering;

public class RaytracingStates
{
    public enum States
    {
        SceneLoading,
        BuildingBVH,
        PrepareRendering,
        Rendering,
        Terminate,
    }

    public static States states = States.Terminate;
    public static States GetRayTracingStates()
    {
        return states;
    }

    public static void SetRayTracingStates(States _states)
    {
        states = _states;
    }

    public static string displayText;
}


//[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class Raytracing : MonoBehaviour
{
    public RaytracingData _RayTracingData;
    public TracingKernel _RaytracingKernel;
    public WavefrontResource wavefrontResource;
    public MegaKernelResource megaResource;
    public DXRPTResource dxrPTResource;
    private Camera cameraComponent;
    public Material _BlitMaterial;
    public Shader _RayConeGBuffer;
    private int cullingMask = 0;
    private bool hasSaveImage = false;
    private RenderTexture defaultCameraTarget;

    void Start()
    {
        RaytracingStates.SetRayTracingStates(RaytracingStates.States.SceneLoading);
        RaytracingStates.displayText = "Loading scene...";
        if (_RayTracingData._kernelType == RaytracingData.KernelType.Wavefront)
        {
            //WavefrontResource wavefrontResource = new WavefrontResource();
            _RaytracingKernel = new WavefrontKernel(wavefrontResource);
        }
        else if (_RayTracingData._kernelType == RaytracingData.KernelType.Mega)
        {
            _RaytracingKernel = new MegaKernel(megaResource);
        }
        else if (_RayTracingData._kernelType == RaytracingData.KernelType.DXR)
        {
            _RaytracingKernel = new DXRKernel(dxrPTResource);
        }

        cameraComponent = GetComponent<Camera>();
        _RayTracingData.Initialize();

        if (_BlitMaterial == null)
        {
            Shader blitShader = Shader.Find("RayTracing/Blit");
            if (blitShader != null)
                _BlitMaterial = new Material(blitShader);

            _BlitMaterial.SetInt("_HDRType", (int)_RayTracingData.HDR);
        }

        cullingMask = cameraComponent.cullingMask;
        //cameraComponent.cullingMask = 0;
        hasSaveImage = false;

        StartCoroutine(_RaytracingKernel.Setup(cameraComponent, _RayTracingData));
    }

    void Update()
    {
        
    }

    void OnDestroy()
    {
        _RaytracingKernel.Release();
        _RayTracingData.Release();
        cameraComponent.cullingMask = cullingMask;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (RaytracingStates.states == RaytracingStates.States.Rendering)
        {
            if (_RaytracingKernel.Update(cameraComponent))
            {
                if (_RayTracingData._SaveOutputTexture)
                {
                    if (!hasSaveImage)
                    {
                        SaveOutputTexture();
                        hasSaveImage = true;
                    }
                }
            }
            if (_BlitMaterial != null)
            {
                _BlitMaterial.SetInt("_HDRType", (int)_RayTracingData.HDR);
            }
        }
        //Graphics.Blit(outputTexture, destination);
        //for gbuffer test
        if (_BlitMaterial != null)
            Graphics.Blit(_RayTracingData.OutputTexture, destination, _BlitMaterial);
        else
            Graphics.Blit(_RayTracingData.OutputTexture, destination);
    }

    private void OnGUI()
    {
        if (_RaytracingKernel != null)
        {
            RaytracingStates.States rayTracingStates = RaytracingStates.GetRayTracingStates();
            switch (rayTracingStates)
            {
                case RaytracingStates.States.Rendering:
                    {
                        //var x = 25;
                        //var y = 20;

                        //GUI.TextArea(new Rect(x, y, 400, 20),
                        //    string.Format("SPP:{0} ", _RaytracingKernel.GetCurrentSPPCount()), GUI.skin.label);
                        break;
                    }
                case RaytracingStates.States.BuildingBVH:
                    {
                        var centeredStyle = GUI.skin.GetStyle("Label");
                        centeredStyle.alignment = TextAnchor.UpperCenter;
                        centeredStyle.fontSize = 30;
                        GUI.Label(new Rect(0, Screen.height / 2 - 25, Screen.width, 50), RaytracingStates.displayText, centeredStyle);
                        break;
                    }
                case RaytracingStates.States.SceneLoading:
                    {
                        var centeredStyle = GUI.skin.GetStyle("Label");
                        centeredStyle.alignment = TextAnchor.UpperCenter;
                        centeredStyle.fontSize = 30;
                        GUI.Label(new Rect(0, Screen.height / 2 - 25, Screen.width, 50), RaytracingStates.displayText, centeredStyle);
                        break;
                    }
                default:
                    break;
            }

        }
    }

    void OnPreRender()
    {
        //cameraComponent.cullingMask = cullingMask;
        //_RaytracingKernel.RenderToGBuffer(cameraComponent);
        //cameraComponent.cullingMask = 0;
        defaultCameraTarget = cameraComponent.targetTexture;
        RenderTexture rayConeGBuffer = _RayTracingData.RayConeGBuffer;
        Graphics.SetRenderTarget(rayConeGBuffer);
        if (_RayConeGBuffer == null )
        {
            _RayConeGBuffer = Shader.Find("RayTracing/RayCone");
        }
        cameraComponent.SetReplacementShader(_RayConeGBuffer, "RenderType");
    }

    private void OnRenderObject()
    {
        Graphics.SetRenderTarget(null);
    }

    void SaveOutputTexture()
    {
        RenderTexture outputTexture = _RayTracingData.OutputTexture;
        Texture2D texture2D = new Texture2D(outputTexture.width, outputTexture.height, TextureFormat.RGBAHalf, false);
        texture2D.filterMode = FilterMode.Bilinear;
        //RenderTexture.active = outputTexture;
        Graphics.SetRenderTarget(outputTexture);
        texture2D.ReadPixels(new Rect(0, 0, texture2D.width, texture2D.height), 0, 0, false);
        Graphics.SetRenderTarget(null);
        texture2D.Apply();
        byte[] bytes = ImageConversion.EncodeArrayToEXR(texture2D.GetRawTextureData(), texture2D.graphicsFormat, (uint)texture2D.width, (uint)texture2D.height, 0, Texture2D.EXRFlags.OutputAsFloat);
        UnityEngine.Object.Destroy(texture2D);
        File.WriteAllBytes(Application.dataPath + "/../" + SceneManager.GetActiveScene().name + ".exr", bytes);
    }
}
