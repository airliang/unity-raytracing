using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.XR;
using System.IO;
using UnityEngine.SceneManagement;

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
    private Camera cameraComponent;
    public Material _BlitMaterial;
    private int cullingMask = 0;
    private bool hasSaveImage = false;

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

        cameraComponent = GetComponent<Camera>();
        

        if (_BlitMaterial == null)
        {
            Shader blitShader = Shader.Find("RayTracing/Blit");
            if (blitShader != null)
                _BlitMaterial = new Material(blitShader);

            _BlitMaterial.SetInt("_HDRType", (int)_RayTracingData.HDR);
        }

        cullingMask = cameraComponent.cullingMask;
        cameraComponent.cullingMask = 0;
        hasSaveImage = false;

        StartCoroutine(_RaytracingKernel.Setup(cameraComponent, _RayTracingData));
    }

    void Update()
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

            if (Input.GetMouseButtonUp(0))
            {
                Vector3 radiance = RayTracingTest.OnePathTracing((int)Input.mousePosition.x, (int)Input.mousePosition.y, (int)Screen.width, 1,
                    _RaytracingKernel.GetGPUSceneData(), _RaytracingKernel.GetGPUFilterData().filter, cameraComponent);
                Vector3 K = new Vector3(3.9747f, 2.38f, 1.5998f);
                Vector3 etaT = new Vector3(0.1428f, 0.3741f, 1.4394f);
                float cosTheta = 0.3f;
                Vector3 fr = RayTracingTest.FrConductor(cosTheta, Vector3.one, etaT, K);
                Debug.Log("cosTheta = " + cosTheta + " fresnel = " + fr);
            }
            else if (Input.GetMouseButtonUp(1))
            {
                Vector3 radiance = RayTracingTest.OnePathTracing(250, 250, (int)Screen.width, 1,
                   _RaytracingKernel.GetGPUSceneData(), _RaytracingKernel.GetGPUFilterData().filter, cameraComponent);
            }
        }
    }

    void OnDestroy()
    {
        _RaytracingKernel.Release();
        MeshRenderer[] meshRenderers = GameObject.FindObjectsOfType<MeshRenderer>();
        cameraComponent.cullingMask = cullingMask;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        //Graphics.Blit(outputTexture, destination);
        //for gbuffer test
        if (_BlitMaterial != null)
            Graphics.Blit(_RaytracingKernel.GetOutputTexture(), destination, _BlitMaterial);
        else
            Graphics.Blit(_RaytracingKernel.GetOutputTexture(), destination);
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

    

    void SaveOutputTexture()
    {
        RenderTexture outputTexture = _RaytracingKernel.GetOutputTexture();
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
